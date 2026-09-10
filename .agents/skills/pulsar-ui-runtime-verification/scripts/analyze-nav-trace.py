#!/usr/bin/env python3
"""分析 Pulsar 指示器导航追踪日志，输出可证伪的签名统计与逐次导航对照表。

用法:
    python analyze-nav-trace.py <trace.log> [<trace-BEFORE.log> ...]

日志由临时追踪通道产生（见 SKILL.md 第 3 步），格式示例:
    11:46:24.363 [NAV] old='Appearance' new='Slots' prevActive='Appearance' animating=False
    11:46:24.388 [REPOS] dpi-changed animating=True
    11:46:24.469 [ANIM] PHASE1 baseTop=207.0 baseH=22.0 -> stretchTop=69.7 stretchH=159.3
    11:46:24.470 [SNAP] caller=InitializeNavIndicator top=207.0->69.7 clearAnim=True animating=True
    11:46:24.599 [ANIM] PHASE2 atTop=69.7 atH=22.0

判据（"修复前 vs 修复后"对照的核心）:
  BAD-1  clearAnim=True 且 animating=True  -> 动画在播放中被摘掉（瞬态的直接元凶）
  BAD-2  PHASE2 起步高度 == 终点高度       -> 已到终点，零可见位移（同样表现为瞬态）
  GOOD   PHASE2 起步高度 明显大于终点高度  -> 动画真的活着
"""
import re
import sys
from collections import Counter, OrderedDict

NAV_RE = re.compile(r"\[NAV\] old='(?P<old>[^']*)' new='(?P<new>[^']*)'")
# PHASE1 有两种历史行样式：早期 `baseTop/baseH`，后续 `startTop/startH`（都兼容）
PHASE1_RE = re.compile(r"\[ANIM\] PHASE1 (?:base|start)Top=(?P<top>[\d.]+) (?:base|start)H=(?P<h>[\d.]+)")
PHASE2_RE = re.compile(r"\[ANIM\] PHASE2 atTop=(?P<at_top>[\d.]+) atH=(?P<at_h>[\d.]+)")
SNAP_RE = re.compile(r"\[SNAP\] caller=(?P<caller>\S+).*clearAnim=(?P<clear>True|False) animating=(?P<anim>True|False)")
REPOS_RE = re.compile(r"\[REPOS\] (?P<kind>[a-z-]+)")
# 守卫抑制行有两种出处：`[ANIM] SKIP <reason>` 与 `[REPOS] SKIP <reason>`
SKIP_RE = re.compile(r"\[(?:ANIM|REPOS)\] SKIP (?P<reason>[A-Za-z-]+)")

# 指示器静止高度（SettingsWindow.xaml.cs 的 IndicatorHeight）。若该值变更需同步修改。
TERMINAL_H = 22.0


def analyze(path):
    navs = []          # 每次导航: dict(old, new, dpi, pane, skip, phase1, phase2)
    cur = None
    counters = Counter()
    phase2_starts = []

    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.rstrip("\n")

            if m := NAV_RE.search(line):
                cur = {"old": m["old"], "new": m["new"], "dpi": 0, "pane": 0, "skip": [], "phase1": None, "phase2": None}
                navs.append(cur)
                continue

            if m := REPOS_RE.search(line):
                kind = m["kind"]
                counters[f"REPOS:{kind}"] += 1
                if cur is not None:
                    if kind == "dpi-changed":
                        cur["dpi"] += 1
                    elif kind.startswith("pane-"):
                        cur["pane"] += 1
                continue

            if m := SKIP_RE.search(line):
                counters[f"SKIP:{m['reason']}"] += 1
                if cur is not None:
                    cur["skip"].append(m["reason"])
                continue

            if m := SNAP_RE.search(line):
                if m["clear"] == "True" and m["anim"] == "True":
                    counters["BAD clearAnim+animating"] += 1
                counters[f"SNAP:{m['caller']}"] += 1
                continue

            if m := PHASE1_RE.search(line):
                counters["PHASE1"] += 1
                if cur is not None:
                    cur["phase1"] = float(m["h"])
                continue

            if m := PHASE2_RE.search(line):
                counters["PHASE2"] += 1
                at_h = float(m["at_h"])
                phase2_starts.append(at_h)
                if cur is not None:
                    cur["phase2"] = at_h
                continue

    return navs, counters, phase2_starts


def report(path):
    navs, counters, phase2_starts = analyze(path)
    print(f"\n{'=' * 78}\n{path}\n{'=' * 78}")

    bad_clear = counters["BAD clearAnim+animating"]
    zero_move = sum(1 for h in phase2_starts if h <= TERMINAL_H)
    completed = sum(1 for n in navs if n["phase1"] is not None and n["phase2"] is not None)

    print("-- 关键签名 --")
    print(f"  BAD-1 动画播放中被摘掉 (clearAnim=True animating=True) : {bad_clear}")
    print(f"  BAD-2 PHASE2 起步高度 <={TERMINAL_H:.0f}（零位移瞬态）              : {zero_move} / {len(phase2_starts)}")
    print(f"  PHASE1 / PHASE2                                        : {counters['PHASE1']} / {counters['PHASE2']}")
    print(f"  导航走完两段                                           : {completed} / {len(navs)}")
    guard = sum(v for k, v in counters.items() if k.startswith("SKIP:animating-suppressed"))
    if guard:
        injected = counters["REPOS:pane-size-changed"]
        print(f"  守卫命中 animating-suppressed                          : {guard}"
              + (f" / {injected} (pane-size-changed 注入)" if injected else ""))
    for k in sorted(counters):
        if k.startswith(("SKIP:", "SNAP:")) and "animating-suppressed" not in k:
            print(f"    {k} = {counters[k]}")

    print("-- 逐次导航 --")
    print(f"  {'出发 -> 目的地':<40} {'dpi':>4} {'pane':>5}  {'skip':<24} phase2起步")
    for n in navs:
        p2 = f"{n['phase2']:.1f}" if n["phase2"] is not None else "-"
        p2 += "  <-- 零位移" if n["phase2"] is not None and n["phase2"] <= TERMINAL_H else ""
        label = f"{n['old'] or '(启动)'} -> {n['new']}"
        if n["skip"]:
            agg = Counter(n["skip"])
            skip = " ".join(f"{k}x{v}" for k, v in agg.items())
        else:
            skip = "-"
        print(f"  {label:<40} {n['dpi']:>4} {n['pane']:>5}  {skip:<24} {p2}")

    verdict = "PASS（无被摘动画 / 无零位移瞬态）" if bad_clear == 0 and zero_move == 0 else "FAIL（见 BAD 判据）"
    print(f"-- 结论 --\n  {verdict}")
    return bad_clear, zero_move, counters


def main():
    paths = sys.argv[1:]
    if not paths:
        print(__doc__)
        return 2
    results = [report(p) for p in paths]
    if len(results) == 2:
        (b0, z0, _), (b1, z1, _) = results
        print(f"\n{'=' * 78}\n对照（前 -> 后）\n{'=' * 78}")
        print(f"  clearAnim=True animating=True : {b0} -> {b1}")
        print(f"  PHASE2 零位移瞬态             : {z0} -> {z1}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
