@echo off
cd /d "E:\8_Project\10_C#\Pulsar_Project\Pulsar\Pulsar.Simulator"
set SIM=bin\Release\net8.0-windows\Pulsar.Simulator.exe

echo ===== 1) bookmarklet dry-run =====
"%SIM%" -plugin com.pulsar.bookmarklet -action run -g "{""code"":""document.querySelector('#login').click()""}"

echo ===== 2) vbarunner format dry-run =====
"%SIM%" -plugin com.pulsar.vbarunner -action run -g "{""scriptPath"":""C:\Users\milo\Documents\Pulsar\Scripts\format-report.bas"",""macro"":""FormatReport""}"

echo ===== 3) pki fill dry-run =====
"%SIM%" -plugin com.pulsar.secretfill -action fill -g "{""secretId"":""ce2a471f-deb7-41c1-9c88-bee2438a50dc"",""autoEnter"":""true""}"

echo ===== 4) vbarunner pack dry-run =====
"%SIM%" -plugin com.pulsar.vbarunner -action run -g "{""scriptPath"":""C:\Users\milo\Documents\Pulsar\Scripts\report-pack.bas"",""macro"":""PackReport""}"

echo ===== 5) winswitcher launch dry-run =====
"%SIM%" -plugin com.pulsar.winswitcher -action launch -g "{""path"":""calc.exe""}"
