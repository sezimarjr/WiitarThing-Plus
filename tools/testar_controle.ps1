# Teste ao vivo do controle Xbox 360 virtual criado pelo WiitarThing
# Mostra trastes, palheta, whammy e inclinacao (Star Power) usando o mapeamento padrao do WiitarThing.
param(
    [switch]$Once,      # mostra uma leitura e sai (para checagem rapida)
    [int]$Player = -1   # 0-3; -1 = primeiro controle conectado
)

Add-Type -TypeDefinition @"
using System.Runtime.InteropServices;
public static class XInputTest
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LX;
        public short LY;
        public short RX;
        public short RY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct State
    {
        public uint Packet;
        public Gamepad Pad;
    }

    [DllImport("xinput1_4.dll")]
    public static extern uint XInputGetState(uint index, out State state);
}
"@

function Find-Player {
    for ($i = 0; $i -lt 4; $i++) {
        $s = New-Object XInputTest+State
        if ([XInputTest]::XInputGetState($i, [ref]$s) -eq 0) { return $i }
    }
    return -1
}

function Get-Bar([double]$fraction, [int]$width = 30) {
    $fraction = [Math]::Max(0, [Math]::Min(1, $fraction))
    $filled = [int][Math]::Round($fraction * $width)
    return '[' + ('#' * $filled) + ('.' * ($width - $filled)) + ']' + (' {0,3}%' -f [int]($fraction * 100))
}

function Show-State([int]$index) {
    $s = New-Object XInputTest+State
    if ([XInputTest]::XInputGetState($index, [ref]$s) -ne 0) {
        Write-Host "Controle $($index + 1) desconectado. Conecte a guitarra no WiitarThing.            " -ForegroundColor Red
        return
    }

    $b = $s.Pad.Buttons
    $frets = @(
        @{ Name = 'VERDE';    Bit = 0x1000; Color = 'Green' },
        @{ Name = 'VERMELHO'; Bit = 0x2000; Color = 'Red' },
        @{ Name = 'AMARELO';  Bit = 0x8000; Color = 'Yellow' },
        @{ Name = 'AZUL';     Bit = 0x4000; Color = 'Blue' },
        @{ Name = 'LARANJA';  Bit = 0x0100; Color = 'DarkYellow' }
    )

    Write-Host ("Controle {0}   (Esc para sair)                                   " -f ($index + 1))
    Write-Host ""
    Write-Host -NoNewline "Trastes:  "
    foreach ($f in $frets) {
        if ($b -band $f.Bit) { Write-Host -NoNewline (" {0} " -f $f.Name) -ForegroundColor Black -BackgroundColor $f.Color }
        else { Write-Host -NoNewline (" {0} " -f $f.Name.ToLower()) -ForegroundColor DarkGray }
        Write-Host -NoNewline " "
    }
    Write-Host "      "

    $strum = if ($b -band 0x0001) { 'PARA CIMA ' } elseif ($b -band 0x0002) { 'PARA BAIXO' } else { '-         ' }
    Write-Host ("Palheta:  {0}" -f $strum)

    $select = if ($b -band 0x0020) { 'APERTADO' } else { '-       ' }
    $start = if ($b -band 0x0010) { 'APERTADO' } else { '-       ' }
    Write-Host ("Select (-): {0}    Start (+): {1}" -f $select, $start)
    Write-Host ""

    # Whammy: analogico direito X, de -32768 (solto) a 32767 (fundo)
    $whammy = ($s.Pad.RX + 32768) / 65535.0
    Write-Host ("Whammy (alavanca):       {0}" -f (Get-Bar $whammy))

    # Inclinacao: analogico direito Y para cima (0 = deitada, 100% = em pe)
    $tilt = [Math]::Max(0, $s.Pad.RY) / 32767.0
    $tiltColor = if ($tilt -ge 0.5) { 'Cyan' } else { 'Gray' }
    Write-Host ("Inclinacao (Star Power): {0}" -f (Get-Bar $tilt)) -ForegroundColor $tiltColor
    $hint = if ($tilt -ge 0.5) { '>> inclinada o suficiente para a maioria dos jogos soltarem o especial' } else { '   levante o braco da guitarra para a barra subir                      ' }
    Write-Host $hint -ForegroundColor $tiltColor
    Write-Host ""
    Write-Host ("Valores brutos: botoes=0x{0:X4}  RX={1,6}  RY={2,6}      " -f $b, $s.Pad.RX, $s.Pad.RY) -ForegroundColor DarkGray
}

$index = if ($Player -ge 0) { $Player } else { Find-Player }
if ($index -lt 0) {
    Write-Host "Nenhum controle Xbox 360 encontrado." -ForegroundColor Red
    Write-Host "Abra o WiitarThing, conecte a guitarra (CONNECT) e rode este teste de novo."
    exit 1
}

if ($Once) {
    Show-State $index
    exit 0
}

Clear-Host
Write-Host "Antes de testar a inclinacao, calibre no WiitarThing:"
Write-Host "  1) guitarra deitada, trastes para cima, braco para a esquerda -> aperte 1 no Wiimote"
Write-Host "  2) guitarra em pe, braco apontando para cima                  -> aperte 2 no Wiimote"
Write-Host ""
$top = [Console]::CursorTop
[Console]::CursorVisible = $false
try {
    while ($true) {
        if ([Console]::KeyAvailable -and [Console]::ReadKey($true).Key -eq 'Escape') { break }
        [Console]::SetCursorPosition(0, $top)
        Show-State $index
        Start-Sleep -Milliseconds 30
    }
}
finally {
    [Console]::CursorVisible = $true
}
