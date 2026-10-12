@echo off
REM ============================================================
REM  atualizar.bat - traz as novidades do autor (JoppieToppie)
REM  para o seu branch KiritoJPK_fixes e envia o backup ao GitHub.
REM  Coloque este arquivo na pasta raiz do projeto.
REM ============================================================
cd /d "%~dp0"

echo.
echo === 1/5 Indo para o branch KiritoJPK_fixes ===
git checkout KiritoJPK_fixes
if errorlevel 1 (
    echo.
    echo ERRO: nao consegui trocar para o KiritoJPK_fixes.
    echo Veja a mensagem acima. Se falar de "local changes", rode:
    echo     git stash push -u -m "backup"
    echo e execute este arquivo de novo.
    pause
    exit /b 1
)

echo.
echo === 2/5 Salvando suas mudancas pendentes ===
set PENDENTE=
for /f "delims=" %%i in ('git status --porcelain') do set PENDENTE=1
if defined PENDENTE (
    git add -A
    git commit -m "Local changes before update"
) else (
    echo Nada pendente.
)

echo.
echo === 3/5 Baixando a versao do autor ===
git fetch origin
if errorlevel 1 (
    echo ERRO: nao consegui baixar do GitHub. Verifique a internet.
    pause
    exit /b 1
)

echo.
echo === 4/5 Juntando com o seu branch ===
git merge origin/main --no-edit
if errorlevel 1 (
    echo.
    echo ============================================================
    echo  ATENCAO: CONFLITO
    echo  O autor mexeu nos mesmos arquivos que voce.
    echo  Arquivos com conflito:
    git diff --name-only --diff-filter=U
    echo.
    echo  Para ficar com a versao do AUTOR em um arquivo:
    echo      git checkout --theirs CAMINHO_DO_ARQUIVO
    echo  Para ficar com a SUA versao:
    echo      git checkout --ours CAMINHO_DO_ARQUIVO
    echo  Depois de resolver todos:
    echo      git add -A
    echo      git commit -m "Merge upstream"
    echo  Para desistir da atualizacao:
    echo      git merge --abort
    echo ============================================================
    pause
    exit /b 1
)

echo.
echo === 5/5 Enviando o backup para o seu GitHub ===
git push meu KiritoJPK_fixes

echo.
echo ============================================================
echo  Pronto! Seu KiritoJPK_fixes esta atualizado.
echo ============================================================
pause
