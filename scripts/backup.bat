@echo off
:: Backup mensal do banco de dados e fotos
:: Executa no Agendador de Tarefas do Windows no dia 1 de cada mês às 02:00

set BACKUP_DIR=%~dp0..\storage\backup_local
set DATE=%date:~6,4%%date:~3,2%

echo [%date% %time%] Iniciando backup...

:: Backup do PostgreSQL
pg_dump -U postgres -F c -f "%BACKUP_DIR%\db_dump_%DATE%.backup" hidrometro_brk

:: Copiar fotos do mês anterior
set PREV_MONTH=%date:~6,4%%date:~3,2%
xcopy /E /I /Y "..\storage\fotos\%PREV_MONTH%" "%BACKUP_DIR%\fotos_%PREV_MONTH%"

echo [%date% %time%] Backup concluído em %BACKUP_DIR%

:: Limpar backups com mais de 6 meses (manter últimos 6)
forfiles /p "%BACKUP_DIR%" /m "db_dump_*.backup" /d -180 /c "cmd /c del @file" 2>nul

echo [%date% %time%] Limpeza de backups antigos concluída.
