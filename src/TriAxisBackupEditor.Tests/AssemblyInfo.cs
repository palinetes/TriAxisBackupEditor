using Xunit;

// O idioma do programa é global (Localizer); os testes que trocam de idioma não podem rodar junto com os outros.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
