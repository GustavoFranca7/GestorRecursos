; Instalador do Gestor de Recursos.
;
; Compilado pelo Inno Setup (gratuito). Gera um unico .exe que ela clica duas
; vezes: instala, cria os atalhos e aparece em "Adicionar ou remover programas".
;
; Instala no perfil do usuario, nao em Arquivos de Programas: assim nao pede
; senha de administrador, que ela nao tem por que digitar para usar o sistema.
;
; ATENCAO: o arquivo gerado contem a senha do banco e a wallet. Nao pode ir
; para o Git nem para lugar publico. Veja LEIA-ME.md.

#define Nome     "Gestor de Recursos"
#define Exe      "GestorRecursos.Desktop.exe"
#define Publicador "Gustavo Franca"

; A versao vem da linha de comando (/DVersao=1.1.0); 1.0.0 e o padrao.
#ifndef Versao
  #define Versao "1.0.0"
#endif

[Setup]
AppId={{B7E4C1A2-9F3D-4E55-8C6B-1D2A3F4E5B60}
AppName={#Nome}
AppVersion={#Versao}
AppPublisher={#Publicador}
DefaultDirName={localappdata}\Programs\GestorRecursos
DefaultGroupName={#Nome}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=saida
OutputBaseFilename=GestorRecursos-{#Versao}-setup
SetupIconFile=..\GestorRecursos.Desktop\Assets\Icone.ico
UninstallDisplayIcon={app}\{#Exe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; O executavel ja vem comprimido (arquivo unico autossuficiente): comprimir de
; novo so faz o instalador demorar para gerar.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na area de trabalho"; GroupDescription: "Atalhos:"

[Files]
; O programa, publicado por publicar.ps1
Source: "saida\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; A carga: configuracao do banco e wallet. Fica fora do Git.
; onlyifdoesntexist no appsettings para uma reinstalacao nao apagar um ajuste
; feito na maquina dela.
Source: "carga\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
Source: "carga\wallet\*"; DestDir: "{app}\wallet"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#Nome}"; Filename: "{app}\{#Exe}"
Name: "{group}\Desinstalar {#Nome}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#Nome}"; Filename: "{app}\{#Exe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#Exe}"; Description: "Abrir o {#Nome} agora"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; A wallet e a configuracao foram trazidas pelo instalador: saem com ele.
Type: filesandordirs; Name: "{app}\wallet"
Type: files; Name: "{app}\appsettings.json"
