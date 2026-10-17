namespace TriAxisBackupEditor.Application.Control;

/// <summary>
/// Bancos no estilo pedaleira (FCB1010): com 10 pedais por banco, banco 0 → programas 1–10, banco 1 → 11–20...
/// O último banco pode ficar incompleto (programas acima de 128 não existem).
/// </summary>
public sealed class BankNavigator
{
    public const int MaxProgram = 128;

    public static IReadOnlyList<int> SupportedBankSizes { get; } = new[] { 4, 5, 8, 10 };

    public BankNavigator(int bankSize)
    {
        if (!SupportedBankSizes.Contains(bankSize))
        {
            throw new ArgumentOutOfRangeException(nameof(bankSize), "Tamanho de banco suportado: 4, 5, 8 ou 10 pedais.");
        }

        BankSize = bankSize;
    }

    public int BankSize { get; }

    /// <summary>Banco atual, começando em 0 (como no display da FCB1010).</summary>
    public int Bank { get; private set; }

    public int BankCount => (MaxProgram + BankSize - 1) / BankSize;

    /// <summary>Programa (1–128) do pedal (1..BankSize) no banco atual, ou null se passar de 128.</summary>
    public int? ProgramFor(int pedal)
    {
        if (pedal < 1 || pedal > BankSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pedal));
        }

        var program = (Bank * BankSize) + pedal;
        return program <= MaxProgram ? program : null;
    }

    /// <summary>Pedal (1..BankSize) que corresponde ao programa, se ele estiver no banco atual.</summary>
    public int? PedalFor(int program)
    {
        var first = (Bank * BankSize) + 1;
        return program >= first && program < first + BankSize ? program - first + 1 : null;
    }

    public void BankUp() => Bank = (Bank + 1) % BankCount;

    public void BankDown() => Bank = (Bank - 1 + BankCount) % BankCount;

    public void SetBank(int bank) => Bank = ((bank % BankCount) + BankCount) % BankCount;

    /// <summary>Vai para o banco que contém o programa.</summary>
    public void ShowProgram(int program)
    {
        if (program is >= 1 and <= MaxProgram)
        {
            Bank = (program - 1) / BankSize;
        }
    }
}
