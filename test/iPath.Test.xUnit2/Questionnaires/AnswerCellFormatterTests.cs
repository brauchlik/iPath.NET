using iPath.Application.Features.Questionnaires;

namespace iPath.Test.xUnit2.Questionnaires;

public class AnswerCellFormatterTests
{
    [Theory]
    [InlineData("boolean", "true", null, null, "true")]
    [InlineData("boolean", "false", null, null, "false")]
    [InlineData("string", "sdc test", null, null, "sdc test")]
    [InlineData("quantity", "12", null, "kg", "12 kg")]
    [InlineData("quantity", "30", null, "days", "30 days")]
    [InlineData("quantity", "12", null, null, "12")]
    [InlineData("coding", "http://snomed.info/sct|21522001", "Abdominal pain", null, "Abdominal pain")]
    [InlineData("coding", "Core biopsy", "Core biopsy", null, "Core biopsy")]
    [InlineData("coding", "http://snomed.info/sct|C18.7", null, null, "http://snomed.info/sct|C18.7")]
    [InlineData("quantity", null, null, "kg", "")]
    [InlineData(null, null, null, null, "")]
    public void Format_renders_the_cell_text(string? valueType, string? value, string? display, string? unit, string expected)
        => AnswerCellFormatter.Format(valueType, value, display, unit).Should().Be(expected);

    [Fact]
    public void JoinValues_joins_repeating_answers_and_drops_empty_ones()
        => AnswerCellFormatter.JoinValues(["Sputum", null, " ", "Fine needle aspiration"])
            .Should().Be("Sputum; Fine needle aspiration");

    [Fact]
    public void JoinValues_returns_empty_for_no_values()
        => AnswerCellFormatter.JoinValues([]).Should().Be(string.Empty);
}
