@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo ============================================
echo  Atualizando com a versao do autor...
echo ============================================
echo.
echo [1/3] Salvando as suas mudancas...
git add -A
git commit -q -m "Minhas mudancas" >/dev/null 2>&1
echo.
echo [2/3] Baixando a atualizacao do autor...
git pull --no-edit origin main
if errorlevel 1 goto conflito
echo.
echo [3/3] Baixando texturas e modelos (LFS)...
git lfs pull
echo.
echo ============================================
echo  Pronto! Tudo atualizado e as suas mudancas foram mantidas.
echo ============================================
pause
exit /b 0

:conflito
echo.
echo ============================================
echo  ATENCAO: CONFLITO
echo  O autor mexeu nos mesmos arquivos que voce.
echo  Arquivos com conflito:
echo ============================================
git diff --name-only --diff-filter=U
echo.
echo  Nada foi perdido. Tire um print desta tela e peca ajuda.
echo  Para desistir e voltar como estava, rode:  git merge --abort
pause
exit /b 1
