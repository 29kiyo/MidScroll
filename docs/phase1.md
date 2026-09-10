# フェーズ1: 環境構築・リポジトリ作成・ドキュメント整備

## 目的
開発環境(VS Code + git bash + .NET SDK)を整え、リポジトリと
プロジェクト雛形、設計ドキュメント一式を用意する。

## 作業内容
- GitHubリポジトリ作成
- VS Code拡張機能インストール(C# Dev Kit等)
- VS Codeの既定ターミナルをgit bashに設定
- .NET 8 SDKの確認
- dotnet new でソリューション/WinFormsプロジェクト雛形作成
- .gitignore作成
- docs/CLAUDE.md, docs/phaseN.md, docs/development_status.md作成

## 完了条件 (Definition of Done)
- リポジトリがGitHubにpushされている
- VS Codeでgit bashターミナルが開ける
- `dotnet build` がsrc/MidScrollで成功する
- docs/以下に設計ドキュメント一式が存在する

## 成果物
- .vscode/settings.json
- src/MidScroll/ (雛形プロジェクト)
- docs/CLAUDE.md, docs/phase1.md〜phase5.md, docs/development_status.md
