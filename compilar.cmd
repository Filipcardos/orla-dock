@echo off
setlocal
cd /d "%~dp0"
where x86_64-w64-mingw32-clang >nul 2>nul || (
    echo Use o clang distribuido com LLVM-MinGW e coloque a pasta bin no PATH.
    exit /b 1
)
x86_64-w64-mingw32-windres orla.rc -O coff -o orla-resource.o || exit /b 1
x86_64-w64-mingw32-clang -std=c17 -O2 -Wall -Wextra -Wpedantic -Werror -municode -mwindows -s -o OrlaDock.exe orla.c orla-resource.o -luser32 -lgdi32 -lshell32 -lole32 -ldwmapi -lcomctl32 -luuid
set "status=%errorlevel%"
del orla-resource.o
exit /b %status%
