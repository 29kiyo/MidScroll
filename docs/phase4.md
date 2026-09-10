# フェーズ4: GitHub Actions CI/CD (ビルド・パッケージング・自動リリース)

## 目的
workflow_dispatchでバージョンタグを入力すると、
zipとインストーラーをビルドし、直接GitHub Releaseとして公開する
build.ymlを作成する。

## 作業内容
- .github/workflows/build.yml作成
  - workflow_dispatch入力: version (例: v1.0.0)
  - dotnet publish (self-contained, win-x64, PublishSingleFile, ReadyToRun)
  - 発行物を MidScroll-<version>-win-x64.zip としてzip化
  - Inno Setupで MidScroll-Setup-<version>.exe を生成
    (installer/installer.iss作成)
  - 生成物のファイル名にworkflow_dispatchで入力したversionを反映
  - Artifactは作成せず、softprops/action-gh-release等でタグ付きリリースを直接作成し、
    zip・インストーラーを添付する

## 完了条件
- Actionsの「Run workflow」でバージョン(例: v1.0.0)を入力して実行すると、
  該当タグでGitHub Releaseが自動作成され、zipとインストーラーが添付される
- ダウンロードしたzip/インストーラーが実機で動作する

## 成果物
- .github/workflows/build.yml
- installer/installer.iss
