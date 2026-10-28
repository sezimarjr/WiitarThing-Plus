# Resume a sessao mais recente gravada pelo monitor do WiitarThing-Plus (WiitarThing.exe --monitor)
param([string]$Folder = (Join-Path $PSScriptRoot "monitor"))

$csv = Get-ChildItem $Folder -Filter "sessao_*.csv" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $csv) {
    Write-Host "Nenhuma sessao encontrada em $Folder" -ForegroundColor Red
    Write-Host "Rode MONITORAR_SESSAO.bat antes de jogar."
    exit 1
}
$eventsFile = $csv.FullName -replace '\.csv$', '_eventos.log'
$rows = Import-Csv $csv.FullName -Delimiter ';'
$events = if (Test-Path $eventsFile) { Get-Content $eventsFile -Encoding UTF8 } else { @() }

# Windows PowerShell 5.1 can't measure a scriptblock, so convert the column to numbers first
function Get-Sum($rows, $column) { $v = ($rows | ForEach-Object { [double]$_.$column } | Measure-Object -Sum).Sum; if ($v) { $v } else { 0 } }
function Get-Max($rows, $column) { $v = ($rows | ForEach-Object { [double]$_.$column } | Measure-Object -Maximum).Maximum; if ($v) { $v } else { 0 } }
function Get-Avg($rows, $column) { $v = ($rows | ForEach-Object { [double]$_.$column } | Measure-Object -Average).Average; if ($v) { $v } else { 0 } }

Write-Host "Sessao: $($csv.BaseName)" -ForegroundColor Cyan
Write-Host ""

foreach ($group in ($rows | Group-Object controle)) {
    $r = $group.Group
    $seconds = $r.Count
    $packets = Get-Sum $r 'pacotes'
    $connected = @($r | Where-Object { [int]$_.pacotes -gt 0 })
    $rate = if ($connected.Count) { $packets / $connected.Count } else { 0 }
    $avgInterval = if ($connected.Count) { Get-Avg $connected 'intervalo_medio_ms' } else { 0 }
    $worst = Get-Max $r 'maior_intervalo_ms'
    $gaps50 = Get-Sum $r 'intervalos_acima_50ms'
    $gaps200 = Get-Sum $r 'intervalos_acima_200ms'
    $deadSeconds = @($r | Where-Object { [int]$_.pacotes -eq 0 }).Count
    $presses = Get-Sum $r 'trastes_apertados'
    $bounces = Get-Sum $r 'trastes_soltaram_e_voltaram'
    $short = Get-Sum $r 'toques_muito_curtos'
    $multi = Get-Sum $r 'muitos_trastes_juntos'
    $strums = Get-Sum $r 'palhetadas'
    $name = $group.Name
    $disconnects = @($events | Where-Object { $_ -like "*$($name): conex*perdida*" }).Count

    Write-Host "=== $name  ($([math]::Floor($seconds / 60)) min $($seconds % 60) s monitorados) ===" -ForegroundColor White
    Write-Host ""
    Write-Host "Bluetooth" -ForegroundColor Cyan
    Write-Host ("  Pacotes por segundo (media):      {0:0}" -f $rate)
    Write-Host ("  Intervalo medio entre pacotes:    {0:0.0} ms" -f $avgInterval)
    Write-Host ("  Maior tempo sem dados:            {0:0} ms" -f $worst)
    Write-Host ("  Travadinhas (mais de 50 ms):      {0}" -f $gaps50)
    Write-Host ("  Travadas (mais de 200 ms):        {0}" -f $gaps200)
    Write-Host ("  Segundos inteiros sem dados:      {0}" -f $deadSeconds)
    Write-Host ("  Quedas de conexao:                {0}" -f $disconnects)

    $btVerdict = if ($disconnects -gt 0 -or $deadSeconds -gt 0) { 'RUIM: a conexao caiu ou ficou parada; aproxime o dongle (extensao USB) e afaste de portas USB 3.0' ; $btColor = 'Red' }
                 elseif ($gaps200 -gt 0 -or $gaps50 -gt ($seconds / 60) * 5) { 'INSTAVEL: houve travadinhas perceptiveis; vale melhorar a posicao do dongle' ; $btColor = 'Yellow' }
                 else { 'BOM: sinal estavel durante a sessao' ; $btColor = 'Green' }
    Write-Host "  => $btVerdict" -ForegroundColor $btColor
    Write-Host ""

    Write-Host "Trastes e palheta" -ForegroundColor Cyan
    Write-Host ("  Trastes apertados:                {0}" -f $presses)
    Write-Host ("  Palhetadas:                       {0}" -f $strums)
    Write-Host ("  Soltaram sozinhos e voltaram:     {0}" -f $bounces)
    Write-Host ("  Toques curtos demais (<12 ms):    {0}" -f $short)
    Write-Host ("  4+ trastes de uma vez (falha):    {0}" -f $multi)

    $fretNames = 'verde', 'vermelho', 'amarelo', 'azul', 'laranja'
    foreach ($fret in $fretNames) {
        $count = @($events | Where-Object { $_ -like "*$($name): traste $fret soltou*" }).Count
        if ($count -gt 0) {
            Write-Host ("    traste {0,-9} soltou sozinho {1} vez(es)" -f $fret, $count) -ForegroundColor Yellow
        }
    }

    $fretVerdict = if ($multi -gt 0) { 'ATENCAO: apareceram varios trastes de uma vez; me mande o arquivo de eventos' ; $fretColor = 'Red' }
                   elseif ($bounces -gt 0) { 'MAU CONTATO provavel nos trastes listados acima (o contato solta por instantes enquanto voce segura)' ; $fretColor = 'Yellow' }
                   else { 'BOM: nenhum traste falhou enquanto estava apertado' ; $fretColor = 'Green' }
    Write-Host "  => $fretVerdict" -ForegroundColor $fretColor
    Write-Host ""
}

$important = @($events | Select-Object -Skip 1)
if ($important.Count -gt 0) {
    Write-Host "Ultimos eventos registrados:" -ForegroundColor Cyan
    $important | Select-Object -Last 15 | ForEach-Object { Write-Host "  $_" }
    Write-Host ""
}

Write-Host "Arquivos completos:" -ForegroundColor DarkGray
Write-Host "  $($csv.FullName)" -ForegroundColor DarkGray
Write-Host "  $eventsFile" -ForegroundColor DarkGray
