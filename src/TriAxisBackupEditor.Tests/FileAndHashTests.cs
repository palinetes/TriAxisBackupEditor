using TriAxisBackupEditor.SysEx;

namespace TriAxisBackupEditor.Tests;

public class HashTests
{
    [Fact]
    public void Sha256_MatchesKnownVector()
    {
        // Vetor de teste FIPS 180-2 para "abc".
        var hash = SysExHash.Sha256Hex("abc"u8);

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public void FileHash_EqualsInMemoryHash()
    {
        var dump = TestDumps.CreateTriAxisDump();
        var path = Path.Combine(TestDumps.TempFolder(), "dump.syx");
        File.WriteAllBytes(path, dump);

        Assert.Equal(SysExHash.Sha256Hex(dump), SysExHash.Sha256HexOfFile(path));
    }

    [Fact]
    public void DifferentDumps_HaveDifferentHashes()
    {
        var a = TestDumps.CreateTriAxisDump(0);
        var b = TestDumps.CreateTriAxisDump(1);

        Assert.NotEqual(SysExHash.Sha256Hex(a), SysExHash.Sha256Hex(b));
        Assert.Equal(64, SysExHash.Sha256Hex(a).Length);
    }
}

public class BackupFileNamerTests
{
    [Fact]
    public void CreateName_UsesRequiredPattern()
    {
        var name = BackupFileNamer.CreateName(new DateTime(2026, 10, 16, 14, 5, 9));

        Assert.Equal("TriAxis_Backup_2026-10-16_14-05-09.syx", name);
    }

    [Fact]
    public void CreateName_IsCultureIndependent()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
            var name = BackupFileNamer.CreateName(new DateTime(2026, 1, 2, 3, 4, 5));
            Assert.Equal("TriAxis_Backup_2026-01-02_03-04-05.syx", name);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void IncompleteName_IsMarkedAndNotSyx()
    {
        var name = BackupFileNamer.CreateIncompleteName(new DateTime(2026, 10, 16, 14, 5, 9));

        Assert.Equal("TriAxis_Backup_2026-10-16_14-05-09_INCOMPLETO.syx.part", name);
        Assert.False(name.EndsWith(".syx", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetUniquePath_NeverReturnsExistingFile()
    {
        var folder = TestDumps.TempFolder();
        var name = BackupFileNamer.CreateName(new DateTime(2026, 10, 16, 14, 5, 9));
        File.WriteAllBytes(Path.Combine(folder, name), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(folder, "TriAxis_Backup_2026-10-16_14-05-09_2.syx"), new byte[] { 2 });

        var path = BackupFileNamer.GetUniquePath(folder, name);

        Assert.Equal(Path.Combine(folder, "TriAxis_Backup_2026-10-16_14-05-09_3.syx"), path);
    }

    [Theory]
    [InlineData("Show São Paulo", "Show São Paulo.syx")]
    [InlineData("a:b*c?.syx", "a_b_c_.syx")]
    [InlineData("  ensaio.SYX  ", "ensaio.SYX")]
    public void SanitizeFileName_KeepsAccentsAndRemovesInvalidChars(string input, string expected)
    {
        Assert.Equal(expected, BackupFileNamer.SanitizeFileName(input));
    }

    [Fact]
    public void SanitizeFileName_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => BackupFileNamer.SanitizeFileName("   "));
    }
}
