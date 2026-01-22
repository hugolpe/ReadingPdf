using System;
using ReadingPdf.Parsers;
using Xunit;

namespace ReadingPdf.Tests.Parsers
{
    public class AmexParserTests
    {
        [Fact]
        public void ExtractPreferredAmount_PicksDollarAmount_WhenConversionHasDollarSign()
        {
            // Original example: final numeric value (no $) + currency label then a $ amount for conversion.
            string block = @"01/15/25 PUERTO MADERO BENITO JUAREZ CANCUN ME
RESTAURANT
4,704.00
Mexican Pesos $230.97";

            var result = AmexParser.ExtractPreferredAmountFromBlock(block);

            Assert.NotNull(result);
            Assert.Equal(230.97m, result.Value); // expect the $-preceded conversion value
        }

        [Fact]
        public void ExtractPreferredAmount_FallsBackToLastAmount_WhenNoDollarSignPresent()
        {
            // No dollar signs — should pick the last numeric amount
            string block = "01/15/25 SOME MERCHANT  $230.97  some text  4,704.00";
            // remove $ from first to simulate no $ present:
            block = block.Replace("$", "");
            var result = AmexParser.ExtractPreferredAmountFromBlock(block);

            Assert.NotNull(result);
            Assert.Equal(4704.00m, result.Value);
        }

        [Fact]
        public void ExtractPreferredAmount_ParsesParenthesesAndDollarSign()
        {
            // Parentheses (negative) with dollar sign present
            string block = "DESCRIPTION ($4,704.00) Mexican Pesos";
            var result = AmexParser.ExtractPreferredAmountFromBlock(block);

            Assert.NotNull(result);
            Assert.Equal(-4704.00m, result.Value);
        }
    }
}