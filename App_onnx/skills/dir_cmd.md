# Skill: DirCommand

## 說明
這個技能會透過 Windows CLI 指令 `dir` 來列舉指定資料夾的內容。

## 指令用法
- `dir <path>` → 顯示檔案與子資料夾。
- `dir <path> /b` → 只顯示檔名。
- `dir <path> /a:d` → 只顯示資料夾。
- `dir <path> /a:-d` → 只顯示檔案。

## 方法
- 使用 `cmd.exe /c dir <path> /b` 執行，取得純檔名清單。
- 使用 `cmd.exe /c dir <path>` 執行，取得完整清單。

## 範例
### 使用者
列出 D:\Projects 的內容

### 模型
{"name":"dir_command","parameters":{"path":"D:\\Projects","options":"/b"}}
