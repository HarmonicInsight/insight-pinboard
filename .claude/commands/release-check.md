# リリースチェックコマンド

対象プロジェクトに対して HARMONIC insight のリリース前チェックを**フェーズ別に対話的**に実行します。

## 重要: 実行ルール

- 各フェーズを**順番に**実行すること。一気に全部やらない。
- 各フェーズ完了後、結果をユーザーに**チェックリスト形式**で提示し、次のフェーズに進むか確認する。
- エラーが見つかった場合、**その場で修正案を提示**し、ユーザーの確認後に修正を実行する。
- TODO ツールを使って全フェーズの進捗を管理する。

## 実行手順

### Phase 0: 準備・環境セットアップ

1. `$ARGUMENTS` が指定されている場合はそのディレクトリ、未指定の場合はカレントディレクトリを対象にする。

2. **insight-common サブモジュールの自動セットアップ**（依存リポジトリの場合）:

   このスキルが insight-common 本体**以外**のリポジトリで実行されている場合、
   以下の手順でサブモジュールを自動的に初期化・更新する。**ユーザーに確認せず自動実行すること。**

   ```bash
   # サブモジュールの存在確認
   if [ -f ".gitmodules" ] && grep -q "insight-common" .gitmodules 2>/dev/null; then
     # サブモジュールが初期化されていない場合
     if [ ! -f "insight-common/CLAUDE.md" ]; then
       echo "insight-common サブモジュールを初期化しています..."
       git submodule init
       git submodule update --recursive
     fi
     # 最新に更新
     echo "insight-common を最新に更新しています..."
     git submodule update --remote --merge insight-common 2>/dev/null || true
   fi
   ```

   サブモジュールのセットアップに失敗した場合はエラーを報告し、手動セットアップの手順を提示する。

3. **スクリプトの実行権限を設定する**:

   ```bash
   chmod +x ./insight-common/scripts/release-check.sh 2>/dev/null || true
   chmod +x ./insight-common/scripts/validate-standards.sh 2>/dev/null || true
   chmod +x ./scripts/release-check.sh 2>/dev/null || true
   chmod +x ./scripts/validate-standards.sh 2>/dev/null || true
   ```

4. プラットフォームを自動検出する:
   - `build.gradle.kts` → Android (Native Kotlin)
   - `app.json` + `expo` → Expo (React Native)
   - `package.json` → React / Next.js
   - `*.csproj` → C# (WPF)
   - `pyproject.toml` / `requirements.txt` → Python
   - `Package.swift` → iOS

5. 検出したプラットフォームをユーザーに通知する。

6. TODO リストに以下のフェーズを登録する:
   - Phase 1: 標準検証（自動スクリプト）
   - Phase 2: コード品質・セキュリティ確認
   - Phase 3: プラットフォーム固有チェック
   - Phase 4: ストアメタデータ確認（モバイルアプリの場合）
   - Phase 5: 最終確認・サマリー

### Phase 1: 標準検証（自動スクリプト）

自動検証スクリプトを実行する。スクリプトのパスは以下の優先順で探す:
- `./scripts/release-check.sh`（insight-common 本体の場合）
- `./insight-common/scripts/release-check.sh`（サブモジュール経由）

```bash
# insight-common 本体
bash ./scripts/release-check.sh ${ARGUMENTS:-.}
# または サブモジュール経由
bash ./insight-common/scripts/release-check.sh ${ARGUMENTS:-.}
```

スクリプトの実行結果を以下のフォーマットでまとめて報告する:

```
## Phase 1: 標準検証 結果

| # | チェック項目 | 結果 | 詳細 |
|---|------------|:----:|------|
| D1 | Gold がプライマリカラー | ✅ / ❌ | ... |
| D2 | Ivory が背景色 | ✅ / ❌ | ... |
| D3 | Blue 未使用 | ✅ / ❌ | ... |
| ... | ... | ... | ... |
```

❌ がある場合はこのフェーズで修正案を提示し、ユーザーの確認を取る。

### Phase 2: コード品質・セキュリティ確認

以下を手動で確認する（Grep ツール等で検索）:

| # | チェック項目 | 確認方法 |
|---|------------|---------|
| Q1 | TODO/FIXME/HACK の残存 | `grep -rn "TODO\|FIXME\|HACK"` |
| Q2 | デバッグ出力の残存 | プラットフォームに応じた検索 |
| Q3 | ハードコードされた API キー | `grep -rn "sk-\|AIza\|AKIA"` |
| S1 | .env が .gitignore に含まれる | `.gitignore` を確認 |
| S2 | credentials ファイルが除外されている | `.gitignore` を確認 |
| G1 | 未コミットの変更がない | `git status` |
| G2 | リモートと同期済み | `git status -sb` |

結果をチェックリスト形式で報告し、問題があればその場で対応する。

**報告フォーマット:**

```
## Phase 2: コード品質・セキュリティ 結果

| # | チェック項目 | 結果 | 詳細 |
|---|------------|:----:|------|
| Q1 | TODO/FIXME/HACK | ✅ / ❌ | 0件 / N件検出 |
| Q2 | デバッグ出力 | ✅ / ❌ | ... |
| Q3 | ハードコード API キー | ✅ / ❌ | ... |
| S1 | .env 除外 | ✅ / ❌ | ... |
| S2 | credentials 除外 | ✅ / ❌ | ... |
| G1 | 未コミット変更なし | ✅ / ❌ | ... |
| G2 | リモート同期 | ✅ / ❌ | ... |
```

### Phase 3: プラットフォーム固有チェック

検出されたプラットフォームに応じて、以下の標準ドキュメントを**読み込んで**チェックリストを実行する:

| プラットフォーム | 参照ドキュメント | チェックリストセクション |
|----------------|----------------|----------------------|
| Android (Native) | `standards/ANDROID.md` | §15 チェックリスト |
| Android (Expo) | `standards/ANDROID.md` | §13.7 チェックリスト |
| iOS | `standards/IOS.md` | リリースチェックリスト |
| C# (WPF) | `standards/CSHARP_WPF.md` | リリースチェックリスト |
| React | `standards/REACT.md` | リリースチェックリスト |
| Python | `standards/PYTHON.md` | リリースチェックリスト |

**注意**: 標準ドキュメントのパスは insight-common 本体か、`insight-common/` サブモジュール配下かを自動判定して読み込むこと。

**Android (Native Kotlin) の場合:**

| # | チェック項目 | 確認方法 | 期待値 |
|---|------------|---------|--------|
| A1 | versionCode | `build.gradle.kts` 確認 | 前回からインクリメント |
| A2 | versionName | `build.gradle.kts` 確認 | セマンティックバージョニング |
| A3 | compileSdk | `build.gradle.kts` 確認 | `35` |
| A4 | targetSdk | `build.gradle.kts` 確認 | `35` |
| A5 | minSdk | `build.gradle.kts` 確認 | `26` |
| A6 | isMinifyEnabled (release) | `build.gradle.kts` release ブロック | `true` |
| A7 | isShrinkResources (release) | `build.gradle.kts` release ブロック | `true` |
| A8 | ProGuard ルール | ファイル存在確認 | 存在する |
| AS1 | release signingConfig | `build.gradle.kts` 確認 | 設定済み |
| AS3 | keystore が .gitignore | `.gitignore` 確認 | 除外済み |

**C# (WPF) の場合:**

| # | チェック項目 | 確認方法 | 期待値 |
|---|------------|---------|--------|
| W1 | AssemblyVersion | `.csproj` 確認 | 更新済み |
| W2 | FileVersion | `.csproj` 確認 | 更新済み |
| LI4 | Syncfusion キー | ソースコード検索 | `third-party-licenses.json` 経由 |
| WF1 | 独自拡張子登録 | インストーラー確認 | 手動確認 |

**React / Next.js の場合:**

| # | チェック項目 | 確認方法 | 期待値 |
|---|------------|---------|--------|
| R1 | package.json version | `package.json` 確認 | 更新済み |
| R2 | TypeScript strict mode | `tsconfig.json` 確認 | `true` |
| R3 | console.log なし | ソースコード検索 | 0件 |
| R4 | 本番環境変数 | 手動確認 | 設定済み |

**Python の場合:**

| # | チェック項目 | 確認方法 | 期待値 |
|---|------------|---------|--------|
| P1 | pyproject.toml version | `pyproject.toml` 確認 | 更新済み |
| P2 | 依存パッケージピン留め | `requirements.txt` 確認 | 全て `==` |
| P3 | テスト通過 | 手動確認 | 成功 |

**Expo / React Native の場合:**

| # | チェック項目 | 確認方法 | 期待値 |
|---|------------|---------|--------|
| E1 | app.json version | `app.json` 確認 | 更新済み |
| E2 | android.versionCode | `app.json` 確認 | インクリメント |
| E3 | eas.json production | `eas.json` 確認 | 存在 |
| E4 | production が app-bundle | `eas.json` 確認 | `app-bundle` |
| E5 | パッケージ名 | `app.json` 確認 | `com.harmonicinsight.*` |

各項目を**1つずつ**確認し、結果をチェックリスト形式で報告する。

**報告フォーマット:**

```
## Phase 3: プラットフォーム固有チェック 結果

| # | チェック項目 | 結果 | 現在の値 |
|---|------------|:----:|---------|
| A1 | versionCode | ✅ | 12 |
| A2 | versionName | ✅ | 1.2.0 |
| ... | ... | ... | ... |
```

### Phase 4: ストアメタデータ確認

モバイルアプリ（Android / iOS / Expo）の場合のみ実行。

**Android (Play Store):**

| # | チェック項目 | 確認方法 |
|---|------------|---------|
| AP1 | `fastlane/metadata/android/ja-JP/title.txt` 存在 (30文字以内) | ファイル確認 + 文字数カウント |
| AP2 | `fastlane/metadata/android/en-US/title.txt` 存在 (30文字以内) | ファイル確認 + 文字数カウント |
| AP3 | `short_description.txt` 日英存在 (80文字以内) | ファイル確認 + 文字数カウント |
| AP4 | `full_description.txt` 日英存在 (4000文字以内) | ファイル確認 + 文字数カウント |
| AP5 | `changelogs/default.txt` 日英存在 (500文字以内) | ファイル確認 + 文字数カウント |
| AP7 | スクリーンショット準備 | ユーザーに確認 |

ファイルが存在しない場合は、テンプレートを提示して作成を提案する。
`standards/LOCALIZATION.md` §6 を参照。

**iOS (App Store):**

| # | チェック項目 | 確認方法 |
|---|------------|---------|
| IA1 | `name.txt` 日英存在 (30文字以内) | ファイル確認 |
| IA2 | `subtitle.txt` 日英存在 (30文字以内) | ファイル確認 |
| IA3 | `description.txt` 日英存在 | ファイル確認 |
| IA5 | `release_notes.txt` 日英存在 | ファイル確認 |
| IA6 | スクリーンショット準備 | ユーザーに確認 |

**報告フォーマット:**

```
## Phase 4: ストアメタデータ 結果

| # | チェック項目 | 結果 | 詳細 |
|---|------------|:----:|------|
| AP1 | ja-JP/title.txt | ✅ | 12文字（上限30） |
| AP2 | en-US/title.txt | ❌ | ファイル未作成 |
| ... | ... | ... | ... |
```

### Phase 5: 最終確認・サマリー

全フェーズの結果を統合し、最終サマリーを表示する:

```
========================================
 リリースチェック 最終サマリー
========================================

対象: [プロジェクトパス]
プラットフォーム: [検出されたプラットフォーム]
実行日時: [日時]

Phase 1: 標準検証         ✅ 完了 (エラー: 0)
Phase 2: コード品質       ✅ 完了 (エラー: 0, 警告: 1)
Phase 3: プラットフォーム  ✅ 完了 (エラー: 0)
Phase 4: ストアメタデータ  ✅ 完了

合計: エラー 0件 / 警告 1件 / 手動確認 3件

手動確認が必要な項目:
1. [ ] スクリーンショットの準備
2. [ ] Release APK/AAB のインストール・動作確認
3. [ ] リリースノートの内容承認

→ 全エラーが解消されていればリリース可能です。
========================================
```

## プラットフォーム別の専用スキル

より詳細なプラットフォーム固有チェックが必要な場合は、専用スキルを使用してください:

- `/release-check-android` — Android (Native Kotlin) 専用の詳細チェック

## 参照ドキュメント

- `standards/RELEASE_CHECKLIST.md` — 全チェック項目の詳細定義（チェック ID 付き）
- `standards/LOCALIZATION.md` — ストアメタデータのローカライゼーション
- `CLAUDE.md` §12 — 開発完了チェックリスト
- `CLAUDE.md` §13 — リリースチェック概要
