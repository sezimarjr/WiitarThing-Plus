@echo off
title Monitor de sessao - WiitarThing-Plus
echo Ligando o monitor de sessao do WiitarThing-Plus...
echo.
echo  - Se o WiitarThing ja estiver aberto, o monitor liga nele sem desconectar a guitarra.
echo  - Se estiver fechado, ele abre ja monitorando. Conecte a guitarra normalmente.
echo.
echo A barra de titulo do WiitarThing-Plus vai mostrar "MONITORANDO SESSAO".
echo Jogue normalmente. Ao terminar, feche o WiitarThing-Plus e rode RESUMO_MONITOR.bat
echo.
start "" "%~dp0WiitarThing.exe" --monitor
timeout /t 8
