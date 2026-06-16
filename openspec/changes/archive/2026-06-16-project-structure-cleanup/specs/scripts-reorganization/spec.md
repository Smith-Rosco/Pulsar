## ADDED Requirements

### Requirement: Move Bookmarklet test scripts to plugin directory
All JavaScript test files in `Scripts/Bookmarklet/` SHALL be moved to `Pulsar/Pulsar/Plugins/Extensions/BookmarkletRunner/TestScripts/`.

#### Scenario: Bookmarklet test scripts are in plugin directory
- **WHEN** inspecting `Pulsar/Pulsar/Plugins/Extensions/BookmarkletRunner/TestScripts/`
- **THEN** the following files SHALL exist: `Flow.js`, `Flow_v2.js`, `temp.html`, `test.js`, `test_complex.js`, `test_error.js`

#### Scenario: Bookmarklet scripts removed from root Scripts/
- **WHEN** inspecting `Scripts/Bookmarklet/`
- **THEN** the Bookmarklet directory SHALL be empty or removed

### Requirement: Move VBA test scripts to plugin directory
All VBA test files in `Scripts/VBA/` SHALL be moved to `Pulsar/Pulsar/Plugins/Extensions/VbaRunner/TestScripts/`.

#### Scenario: VBA test scripts are in plugin directory
- **WHEN** inspecting `Pulsar/Pulsar/Plugins/Extensions/VbaRunner/TestScripts/`
- **THEN** the following files SHALL exist: `FlowSentinel.txt`, `GenerateReversionFlow.txt`, `MacroFlow_DiffTool_Optimized.txt`, `test_vba.txt`

#### Scenario: VBA scripts removed from root Scripts/
- **WHEN** inspecting `Scripts/VBA/`
- **THEN** the VBA directory SHALL be empty or removed

### Requirement: Move icon generation script to brand assets
The Python script `Scripts/make_pulsar_ico.py` SHALL be moved to `Pulsar/Pulsar/Assets/Brand/make_pulsar_ico.py`.

#### Scenario: Icon script is in brand assets
- **WHEN** inspecting `Pulsar/Pulsar/Assets/Brand/`
- **THEN** `make_pulsar_ico.py` SHALL exist

#### Scenario: Icon script removed from root Scripts/
- **WHEN** inspecting `Scripts/`
- **THEN** `make_pulsar_ico.py` SHALL NOT exist

### Requirement: Move demo scripts to plugin directories
Demo scripts in `Pulsar/Pulsar/Assets/Scripts/Demo/` SHALL be moved to their respective plugin directories: `browser_demo.js` → `BookmarkletRunner/DemoScripts/`, `excel_demo.txt` → `VbaRunner/DemoScripts/`.

#### Scenario: Demo scripts are in plugin directories
- **WHEN** inspecting `Pulsar/Pulsar/Plugins/Extensions/BookmarkletRunner/DemoScripts/`
- **THEN** `browser_demo.js` SHALL exist
- **WHEN** inspecting `Pulsar/Pulsar/Plugins/Extensions/VbaRunner/DemoScripts/`
- **THEN** `excel_demo.txt` SHALL exist

#### Scenario: Demo scripts removed from Assets/Scripts/
- **WHEN** inspecting `Pulsar/Pulsar/Assets/Scripts/Demo/`
- **THEN** the directory SHALL be empty or removed

### Requirement: Update AGENTS.md for script paths
The `AGENTS.md` file SHALL be updated if it references any moved script paths.

#### Scenario: AGENTS.md script paths are accurate
- **WHEN** searching `AGENTS.md` for moved file paths
- **THEN** any references to old script locations SHALL be updated to new locations

### Requirement: Project builds after scripts reorganization
File renames SHALL NOT affect any C# or XAML source files.

#### Scenario: Build passes after scripts reorganization
- **WHEN** running `dotnet build Pulsar/Pulsar/Pulsar.csproj`
- **THEN** the build SHALL succeed with zero errors
