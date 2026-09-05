using System.Globalization;
using System.Windows;
using FluentAssertions;
using Pulsar.Core.Converters;
using Pulsar.Models;
using Xunit;

namespace Pulsar.Tests.Converters
{
    public class SortArrowVisibilityConverterTests
    {
        private readonly SortArrowVisibilityConverter _converter = new();

        private object Convert(SortColumn column, bool ascending, string parameter)
        {
            return _converter.Convert(new object[] { column, ascending }, typeof(Visibility), parameter, CultureInfo.InvariantCulture);
        }

        [Theory]
        [InlineData(SortColumn.Executions, true, "Executions|Up", Visibility.Visible)]
        [InlineData(SortColumn.Executions, false, "Executions|Up", Visibility.Collapsed)]
        [InlineData(SortColumn.Executions, false, "Executions|Down", Visibility.Visible)]
        [InlineData(SortColumn.Executions, true, "Executions|Down", Visibility.Collapsed)]
        [InlineData(SortColumn.SuccessRate, true, "Executions|Up", Visibility.Collapsed)]
        [InlineData(SortColumn.Duration, true, "Duration|Up", Visibility.Visible)]
        [InlineData(SortColumn.LastUsed, false, "LastUsed|Down", Visibility.Visible)]
        [InlineData(SortColumn.Executions, true, "LastUsed|Up", Visibility.Collapsed)]
        public void Convert_ReturnsExpectedVisibility(SortColumn column, bool ascending, string parameter, Visibility expected)
        {
            Convert(column, ascending, parameter).Should().Be(expected);
        }

        [Fact]
        public void Convert_MalformedParameter_ReturnsCollapsed()
        {
            Convert(SortColumn.Executions, true, "Executions").Should().Be(Visibility.Collapsed);
            Convert(SortColumn.Executions, true, "Executions|Up|Extra").Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_MissingValues_ReturnsCollapsed()
        {
            _converter.Convert(new object[] { SortColumn.Executions }, typeof(Visibility), "Executions|Up", CultureInfo.InvariantCulture)
                .Should().Be(Visibility.Collapsed);
        }
    }
}
