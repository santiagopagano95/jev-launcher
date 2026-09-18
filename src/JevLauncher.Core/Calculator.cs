using System.Globalization;

namespace JevLauncher.Core;

public static class Calculator
{
    public static bool TryParse(string query, out double value)
    {
        value = 0;
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0) return false;

        if (text.StartsWith("calc ", StringComparison.OrdinalIgnoreCase))
            text = text[5..].Trim();
        else if (text.StartsWith("=", StringComparison.Ordinal))
            text = text[1..].Trim();

        if (!LooksLikeMath(Normalize(text))) return false;

        try
        {
            var parser = new Parser(Normalize(text));
            var result = parser.ParseExpression();
            parser.ExpectEnd();
            if (double.IsNaN(result) || double.IsInfinity(result)) return false;
            value = result;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool LooksLikeMath(string s)
    {
        var hasDigit = s.Any(char.IsDigit);
        var hasOperator = s.IndexOfAny(new[] { '+', '-', '*', '/', '^', '%', '(', ')' }) >= 0
                          || s.Contains("sqrt", StringComparison.OrdinalIgnoreCase);
        return hasDigit && hasOperator;
    }

    private static string Normalize(string s)
    {
        var t = s.Replace("**", "^").Replace(" x ", "*").Replace("X", "*");
        // "15% of 240" -> "15% * 240" ; "200 * 10%" kept
        t = System.Text.RegularExpressions.Regex.Replace(t, @"%\s*of\s*", "%*", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\bsqrt\b", "sqrt", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return t.Trim();
    }

    private sealed class Parser
    {
        private readonly string _s;
        private int _i;

        public Parser(string s) => _s = s;

        public void ExpectEnd()
        {
            SkipWs();
            if (_i != _s.Length) throw new FormatException();
        }

        public double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWs();
                if (Peek('+')) { _i++; value += ParseTerm(); }
                else if (Peek('-')) { _i++; value -= ParseTerm(); }
                else return value;
            }
        }

        private double ParseTerm()
        {
            var value = ParseUnary();
            while (true)
            {
                SkipWs();
                if (Peek('*')) { _i++; value *= ParseUnary(); }
                else if (Peek('/')) { _i++; value /= ParseUnary(); }
                else return value;
            }
        }

        private double ParseUnary()
        {
            SkipWs();
            if (Peek('-')) { _i++; return -ParseUnary(); }
            if (Peek('+')) { _i++; return ParseUnary(); }
            return ParsePower();
        }

        private double ParsePower()
        {
            var left = ParseAtom();
            SkipWs();
            if (Peek('^')) { _i++; return Math.Pow(left, ParseUnary()); }
            return left;
        }

        private double ParseAtom()
        {
            SkipWs();
            if (Peek('('))
            {
                _i++;
                var v = ParseExpression();
                SkipWs();
                if (!Peek(')')) throw new FormatException();
                _i++;
                return ParsePostfixPercent(v);
            }

            if (MatchWord("sqrt"))
            {
                return ParsePostfixPercent(Math.Sqrt(ParseUnary()));
            }

            var start = _i;
            while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.')) _i++;
            if (start == _i) throw new FormatException();
            if (!double.TryParse(_s[start.._i], NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                throw new FormatException();
            return ParsePostfixPercent(num);
        }

        private double ParsePostfixPercent(double v)
        {
            SkipWs();
            if (Peek('%'))
            {
                _i++;
                return v / 100.0;
            }
            return v;
        }

        private bool MatchWord(string word)
        {
            SkipWs();
            if (_i + word.Length <= _s.Length &&
                string.Equals(_s.Substring(_i, word.Length), word, StringComparison.OrdinalIgnoreCase))
            {
                _i += word.Length;
                return true;
            }
            return false;
        }

        private void SkipWs()
        {
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
        }

        private bool Peek(char c) => _i < _s.Length && _s[_i] == c;
    }
}
