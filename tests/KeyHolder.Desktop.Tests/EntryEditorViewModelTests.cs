using KeyHolder.Cryptography;
using KeyHolder.Domain.Entries;
using KeyHolder.Desktop.ViewModels;

namespace KeyHolder.Desktop.Tests;

public sealed class EntryEditorViewModelTests
{
    [Fact]
    public async Task EmptyTitleBlocksSavingAndShowsAnError()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();
        harness.Vault.NewCommand.Execute(null);
        var editor = harness.Vault.Editor!;

        editor.Title = "x";
        editor.Title = "   ";
        editor.SaveCommand.Execute(null);

        Assert.True(editor.HasErrors);
        Assert.NotEmpty(editor.GetErrors(nameof(EntryEditorViewModel.Title)).Cast<string>());
        Assert.NotEmpty(editor.ValidationSummary);
        Assert.Same(editor, harness.Vault.Editor);
        Assert.Equal(10, harness.Vault.Entries.Count);
    }

    [Fact]
    public async Task InvalidImportanceBlocksSaving()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();
        harness.Vault.NewCommand.Execute(null);
        var editor = harness.Vault.Editor!;
        editor.Title = "Fake Valid";

        editor.ImportanceText = "high";
        Assert.True(editor.HasErrors);

        editor.ImportanceText = "500";
        Assert.True(editor.HasErrors);

        editor.ImportanceText = "7";
        Assert.False(editor.HasErrors);
    }

    [Fact]
    public async Task EditingOnlyMetadataKeepsPasswordAndNotes()
    {
        var harness = await Harness.CreateAsync();
        var item = await harness.SelectAsync("Fake Bank");

        harness.Vault.EditCommand.Execute(null);
        var editor = harness.Vault.Editor!;
        Assert.Equal(string.Empty, editor.Password);
        editor.Title = "Fake Bank Renamed";
        editor.SaveCommand.Execute(null);

        var stored = await harness.Repository.GetAsync(item.Id);
        Assert.Equal("Fake Bank Renamed", stored?.Metadata.Title);
        Assert.Equal("FAKE-bank-password-2", stored?.Secret.Password);
        Assert.Equal("FAKE DATA: pretend security answers.", stored?.Secret.Notes);
    }

    [Fact]
    public async Task EditingNotesKeepsThePassword()
    {
        var harness = await Harness.CreateAsync();
        var item = await harness.SelectAsync("Fake Bank");

        harness.Vault.EditCommand.Execute(null);
        harness.Vault.Editor!.Notes = "FAKE DATA: changed note";
        harness.Vault.Editor.SaveCommand.Execute(null);

        var stored = await harness.Repository.GetAsync(item.Id);
        Assert.Equal("FAKE-bank-password-2", stored?.Secret.Password);
        Assert.Equal("FAKE DATA: changed note", stored?.Secret.Notes);
    }

    [Fact]
    public async Task EnteringAPasswordReplacesItAndKeepsTheNotes()
    {
        var harness = await Harness.CreateAsync();
        var item = await harness.SelectAsync("Fake Bank");

        harness.Vault.EditCommand.Execute(null);
        harness.Vault.Editor!.Password = "FAKE-replaced";
        harness.Vault.Editor.SaveCommand.Execute(null);

        var stored = await harness.Repository.GetAsync(item.Id);
        Assert.Equal("FAKE-replaced", stored?.Secret.Password);
        Assert.Equal("FAKE DATA: pretend security answers.", stored?.Secret.Notes);
    }

    [Fact]
    public async Task GenerateFillsThePasswordFieldWithAStrongPassword()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();
        harness.Vault.NewCommand.Execute(null);
        var editor = harness.Vault.Editor!;
        editor.GeneratorLength = 24;

        editor.GenerateCommand.Execute(null);

        Assert.Equal(24, editor.Password.Length);
        Assert.NotEmpty(editor.GeneratorMessage);
    }

    [Fact]
    public async Task GenerateWithAnInvalidLengthShowsAMessageInsteadOfCrashing()
    {
        var harness = await Harness.CreateAsync();
        await harness.Vault.LoadAsync();
        harness.Vault.NewCommand.Execute(null);
        var editor = harness.Vault.Editor!;
        editor.GeneratorLength = PasswordGeneratorOptions.MinLength - 1;

        editor.GenerateCommand.Execute(null);

        Assert.Equal(string.Empty, editor.Password);
        Assert.Contains("Length", editor.GeneratorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EditorOpensWithoutExposingTheStoredPassword()
    {
        var harness = await Harness.CreateAsync();
        await harness.SelectAsync("Fake Bank");

        harness.Vault.EditCommand.Execute(null);

        Assert.Equal(string.Empty, harness.Vault.Editor!.Password);
        Assert.Equal("Leave empty to keep the current password.", harness.Vault.Editor.PasswordHint);
    }
}
