using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Seguranca;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Converte uma senha no hash que vai no INSERT do banco.</summary>
/// <remarks>
/// Existe porque o acesso nasce no banco, pela mão de quem administra, e o SQL não tem
/// como calcular PBKDF2. Sem isto, a alternativa seria uma senha inicial igual para todo
/// mundo, escrita no script versionado.
///
/// Aberto só por <c>--hash</c> na linha de comando: é ferramenta de quem instala, não
/// tela do sistema. Some quando a tela de usuários existir, em Configurações.
/// </remarks>
public partial class GeradorDeHashViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Senha { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Hash { get; set; }

    public bool TemHash => !string.IsNullOrEmpty(Hash);

    [RelayCommand]
    private void Gerar()
    {
        Hash = string.IsNullOrEmpty(Senha) ? null : Senhas.Embaralhar(Senha);
        OnPropertyChanged(nameof(TemHash));
    }
}
