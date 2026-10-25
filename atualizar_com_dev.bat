@echo off
setlocal EnableExtensions
REM ============================================================
REM  atualizar_com_dev.bat
REM  Deixa o KiritoJPK_fixes igual a versao mais recente do dev
REM  (JoppieToppie/main) e coloca por cima so os bots do hangar
REM  Terran (um de cada vez, invisiveis entre os voos):
REM    Assets\Scripts\Runtime\Space\World\StationLevel.cs (a do dev + os trechos dos bots)
REM    Assets\Scripts\Runtime\Space\World\TerranHangarBots.cs (+ .meta)
REM  Antes, tudo o que voce tem fica salvo no branch de backup.
REM  FECHE O UNITY antes de rodar.
REM ============================================================

if /i "%~1"=="run" goto run
REM O proprio .bat e removido do projeto no reset: roda uma copia a partir da pasta temporaria.
set "WORK=%TEMP%\gof2_atualizar_dev"
if exist "%WORK%" rmdir /s /q "%WORK%"
mkdir "%WORK%"
copy /y "%~f0" "%WORK%\atualizar_com_dev.bat" >nul
xcopy /e /i /y "%~dp0_atualizacao" "%WORK%\_atualizacao" >nul
if errorlevel 1 (
    echo ERRO: a pasta _atualizacao nao esta ao lado deste arquivo.
    pause
    exit /b 1
)
call "%WORK%\atualizar_com_dev.bat" run "%~dp0."
exit /b

:run
set "REPO=%~f2"
set "WORK=%~dp0"
set "BACKUP=backup-antes-dev-2026-10-25"
cd /d "%REPO%"

echo.
echo === 1/7 Indo para o branch KiritoJPK_fixes ===
git checkout KiritoJPK_fixes
if errorlevel 1 goto erro

REM Arquivos marcados como skip-worktree nao seriam atualizados pelo reset.
for /f "tokens=1,*" %%a in ('git ls-files -v ^| findstr /b /c:"S "') do git update-index --no-skip-worktree -- "%%b"

REM Arquivos seus que o git nao rastreia (PR_PENDENTES.md, atualizar.bat...): voltam depois do reset (menos os que o dev tambem tem).
git ls-files --others --exclude-standard > "%WORK%\nao_rastreados.txt"

echo.
echo === 2/7 Salvando tudo no branch %BACKUP% ===
git add -A
git commit -q -m "Backup: everything before updating to the dev's latest"
git branch -f %BACKUP% HEAD
if errorlevel 1 goto erro
echo Backup salvo em %BACKUP%.

echo.
echo === 3/7 Baixando a versao do dev ===
git fetch origin
if errorlevel 1 goto erro

echo.
echo === 4/7 Deixando o KiritoJPK_fixes igual ao dev ===
git reset --hard origin/main
if errorlevel 1 goto erro

echo.
echo === 5/7 Devolvendo seus arquivos nao rastreados ===
for /f "usebackq delims=" %%f in ("%WORK%\nao_rastreados.txt") do (
    git ls-files --error-unmatch -- "%%f" >nul 2>&1 || (git checkout %BACKUP% -- "%%f" 2>nul && git reset -q -- "%%f")
)

echo.
echo === 6/7 Colocando os bots do hangar Terran ===
copy /y "%WORK%\_atualizacao\StationLevel.cs" "Assets\Scripts\Runtime\Space\World\StationLevel.cs" >nul || goto erro
copy /y "%WORK%\_atualizacao\TerranHangarBots.cs" "Assets\Scripts\Runtime\Space\World\TerranHangarBots.cs" >nul || goto erro
copy /y "%WORK%\_atualizacao\TerranHangarBots.cs.meta" "Assets\Scripts\Runtime\Space\World\TerranHangarBots.cs.meta" >nul || goto erro
git add "Assets/Scripts/Runtime/Space/World/StationLevel.cs" "Assets/Scripts/Runtime/Space/World/TerranHangarBots.cs" "Assets/Scripts/Runtime/Space/World/TerranHangarBots.cs.meta"
git commit -q -m "Terran hangar: the four service bots fly one at a time, in random order" -m "Each bot (its _anim and _anim_add layers) is hidden until TerranHangarBots picks it, never the one that just flew; it shows while its animation plays and hides again at its end, the next one 0.5-1.5 s later."
if errorlevel 1 goto erro
if exist "_atualizacao" rmdir /s /q "_atualizacao"

echo.
echo === 7/7 Enviando para o seu GitHub ===
git push meu %BACKUP%
git push --force-with-lease meu KiritoJPK_fixes

echo.
echo ============================================================
echo  Pronto! KiritoJPK_fixes = versao do dev + os bots.
echo  Tudo o que voce tinha antes esta no branch %BACKUP%.
echo  Pode apagar este .bat. Abra o Unity.
echo ============================================================
pause
exit /b 0

:erro
echo.
echo ERRO: veja a mensagem acima. Nada do que voce tinha foi perdido
echo (se o passo 2 passou, esta tudo no branch %BACKUP%).
pause
exit /b 1
