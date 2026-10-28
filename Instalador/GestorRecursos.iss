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
; O AppId e o que faz a atualizacao funcionar: o Windows reconhece que ja ha
; esta mesma aplicacao instalada e o instalador novo substitui a anterior, na
; mesma pasta e na mesma linha de "Adicionar ou remover programas". Trocar este
; numero criaria uma segunda instalacao ao lado da primeira.
AppId={{B7E4C1A2-9F3D-4E55-8C6B-1D2A3F4E5B60}
AppName={#Nome}
AppVersion={#Versao}
AppVerName={#Nome} {#Versao}
AppPublisher={#Publicador}
VersionInfoVersion={#Versao}
DefaultDirName={localappdata}\Programs\GestorRecursos
DefaultGroupName={#Nome}
DisableProgramGroupPage=yes
; A pasta e fixa e dentro do perfil: perguntar onde instalar so adiciona um
; passo em que nao ha nada a decidir.
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir=saida
OutputBaseFilename=GestorRecursos-{#Versao}-setup
SetupIconFile=..\GestorRecursos.Desktop\Assets\Icone.ico
UninstallDisplayIcon={app}\{#Exe}
UninstallDisplayName={#Nome} {#Versao}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; O executavel ja vem comprimido (arquivo unico autossuficiente): comprimir de
; novo so faz o instalador demorar para gerar.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Atualizar com o programa aberto e o caso comum: ela clica no instalador novo
; sem lembrar de fechar o sistema. Sem isto, o instalador nao consegue
; substituir o .exe em uso e exige reiniciar o Windows.
CloseApplications=yes
CloseApplicationsFilter=*.exe
; Nao reabrir sozinho: a ultima tela ja oferece abrir, e reabrir no meio da
; instalacao travaria os arquivos de novo.
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na area de trabalho"; GroupDescription: "Atalhos:"

[InstallDelete]
; Sobras de instalacoes antigas. A publicacao em arquivo unico nao deixa .dll
; nem .json de runtime soltos, mas uma versao anterior pode ter deixado, e
; arquivo velho ao lado do executavel novo e fonte de erro dificil de achar.
;
; Os nomes sao citados um a um de proposito: um "*.json" aqui apagaria o
; appsettings.json com a configuracao do banco.
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.pdb"
Type: files; Name: "{app}\GestorRecursos.Desktop.deps.json"
Type: files; Name: "{app}\GestorRecursos.Desktop.runtimeconfig.json"

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
