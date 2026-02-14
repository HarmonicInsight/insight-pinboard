# 開発ガイド

> このプロジェクトは `insight-common/CLAUDE.md` の全標準に準拠します。
> 開発・レビュー・リリース前に必ず参照してください。

## 標準ドキュメント

```bash
cat insight-common/CLAUDE.md                          # 全体ガイド
cat insight-common/standards/RELEASE_CHECKLIST.md     # リリースチェック
```

## 検証コマンド

```bash
# 開発中の標準検証
./insight-common/scripts/validate-standards.sh .

# リリース前の包括チェック
./insight-common/scripts/release-check.sh .
```

## AI アシスタント自動行動ルール

| トリガー | アクション |
|---------|----------|
| 「リリース」「デプロイ」「公開」「本番」「ship」「release」 | `/release-check` を提案・実行 |
| 「PR 作って」「プルリク」「マージ」 | `/release-check` の実行を推奨 |
| 新規 UI 実装 | Ivory & Gold デザイン標準を確認 |
