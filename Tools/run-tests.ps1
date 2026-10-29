# EditModeテストをバッチモードで実行し、結果の要約を表示する。
# 使い方: プロジェクトフォルダで .\Tools\run-tests.ps1
# (Unityエディタでこのプロジェクトを開いている間は実行できません)
$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
$out = Join-Path $project "Logs\test-results.xml"
if (Test-Path $out) { Remove-Item $out }

unity test $project --editor-version 6000.5.8f1 --mode EditMode --output $out --timeout 900 --no-banner --non-interactive
$code = $LASTEXITCODE

if (Test-Path $out) {
    $x = New-Object xml
    $x.Load($out)
    $r = $x.'test-run'
    Write-Output "合計=$($r.total) 成功=$($r.passed) 失敗=$($r.failed)"
    foreach ($tc in $x.SelectNodes('//test-case')) {
        if ($tc.result -ne 'Passed') {
            Write-Output "[$($tc.result)] $($tc.fullname)"
            Write-Output $tc.failure.message.InnerText
        }
    }
} else {
    Write-Output "テスト結果が出力されませんでした(コンパイルエラーの可能性)。Editor.log を確認してください。"
}
exit $code
