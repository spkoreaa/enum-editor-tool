# Enum Editor

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Unity Version](https://img.shields.io/badge/Unity-2018.1%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![GitHub issues](https://img.shields.io/github/issues/spkoreaa/enumeditor.svg)](https://github.com/spkoreaa/enumeditor/issues)
[![Version](https://img.shields.io/badge/version-1.0.1-green.svg)](https://github.com/spkoreaa/enumeditor/releases)

**Enum Editor** is a Unity Editor Window tool for easily creating and editing enums directly within your C# scripts. It streamlines managing enums without manually editing script files and helps improve productivity.

---

## Features

- Load all enums from a selected C# script.
- Add, edit, and remove enum entries with a user-friendly interface.
- Add new enums to your script.
- Save changes directly back to the script file.
- Retains selection and scroll position after saving.
- Validates enum names and prevents duplicates.
- Lightweight and simple editor extension.

---

## Installation

### Via Git (Unity Package Manager)

Add the following line to your project's `Packages/manifest.json` under `dependencies`:

```json
"com.spkorea.enumeditor": "https://github.com/spkoreaa/enum-editor-tool.git"
