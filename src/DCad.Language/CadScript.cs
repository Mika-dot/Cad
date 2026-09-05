using DCad.Core;
using System.Globalization;

namespace DCad.Language;

public sealed class CadScriptResult : IDisposable
{
    private readonly List<IDisposable> _owned;
    public ISolid Result { get; }
    public IReadOnlyDictionary<string, double> Parameters { get; }

    internal CadScriptResult(ISolid result, Dictionary<string, double> parameters, List<IDisposable> owned)
    {
        Result = result;
        Parameters = parameters;
        _owned = owned;
    }

    public void Dispose()
    {
        foreach (var item in Enumerable.Reverse(_owned)) item.Dispose();
    }
}

public static class CadScript
{
    public static CadScriptResult Execute(string source, IModelingKernel kernel)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(kernel);
        return new Parser(source, kernel).Execute();
    }

    private enum TokenKind { Identifier, Number, Let, Param, Solid, Plus, Minus, Ampersand, Equal, LParen, RParen, Comma, Semicolon, End }
    private readonly record struct Token(TokenKind Kind, string Text, double Number = 0);
    private readonly record struct Value(double? Scalar, ISolid? Solid)
    {
        public double RequireScalar(string context) => Scalar ?? throw new InvalidOperationException($"{context} requires a scalar value.");
        public ISolid RequireSolid(string context) => Solid ?? throw new InvalidOperationException($"{context} requires a solid value.");
        public static Value From(double x) => new(x, null);
        public static Value From(ISolid x) => new(null, x);
    }

    private sealed class Lexer
    {
        private readonly string _text;
        private int _i;
        public Lexer(string text) => _text = text;

        public Token Next()
        {
            SkipTrivia();
            if (_i >= _text.Length) return new(TokenKind.End, "");
            var c = _text[_i];
            _i++;
            return c switch
            {
                '+' => new(TokenKind.Plus, "+"),
                '-' => new(TokenKind.Minus, "-"),
                '&' => new(TokenKind.Ampersand, "&"),
                '=' => new(TokenKind.Equal, "="),
                '(' => new(TokenKind.LParen, "("),
                ')' => new(TokenKind.RParen, ")"),
                ',' => new(TokenKind.Comma, ","),
                ';' => new(TokenKind.Semicolon, ";"),
                _ when char.IsLetter(c) || c == '_' => ReadIdentifier(c),
                _ when char.IsDigit(c) || c == '.' => ReadNumber(c),
                _ => throw Error($"Unexpected character '{c}'.")
            };
        }

        private Token ReadIdentifier(char first)
        {
            var start = _i - 1;
            while (_i < _text.Length && (char.IsLetterOrDigit(_text[_i]) || _text[_i] == '_')) _i++;
            var s = _text[start.._i];
            return s switch
            {
                "let" => new(TokenKind.Let, s),
                "param" => new(TokenKind.Param, s),
                "solid" => new(TokenKind.Solid, s),
                _ => new(TokenKind.Identifier, s)
            };
        }

        private Token ReadNumber(char first)
        {
            var start = _i - 1;
            var sawExponent = false;
            while (_i < _text.Length)
            {
                var c = _text[_i];
                if (char.IsDigit(c) || c == '.') { _i++; continue; }
                if ((c == 'e' || c == 'E') && !sawExponent)
                {
                    sawExponent = true; _i++;
                    if (_i < _text.Length && (_text[_i] == '+' || _text[_i] == '-')) _i++;
                    continue;
                }
                break;
            }
            var numberText = _text[start.._i];
            if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw Error($"Invalid number '{numberText}'.");

            var unitStart = _i;
            while (_i < _text.Length && char.IsLetter(_text[_i])) _i++;
            var unit = _text[unitStart.._i];
            value *= unit switch
            {
                "" or "mm" or "deg" => 1.0,
                "cm" => 10.0,
                "m" => 1000.0,
                _ => throw Error($"Unknown unit '{unit}'. Supported: mm, cm, m, deg.")
            };
            return new(TokenKind.Number, numberText + unit, value);
        }

        private void SkipTrivia()
        {
            while (_i < _text.Length)
            {
                if (char.IsWhiteSpace(_text[_i])) { _i++; continue; }
                if (_text[_i] == '#')
                {
                    while (_i < _text.Length && _text[_i] != '\n') _i++;
                    continue;
                }
                if (_i + 1 < _text.Length && _text[_i] == '/' && _text[_i + 1] == '/')
                {
                    _i += 2;
                    while (_i < _text.Length && _text[_i] != '\n') _i++;
                    continue;
                }
                break;
            }
        }

        private FormatException Error(string message) => new($"CAD script lexical error near character {_i}: {message}");
    }

    private sealed class Parser
    {
        private readonly IModelingKernel _kernel;
        private readonly Lexer _lexer;
        private Token _current;
        private readonly Dictionary<string, Value> _symbols = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _parameters = new(StringComparer.Ordinal);
        private readonly List<IDisposable> _owned = new();
        private ISolid? _result;

        public Parser(string text, IModelingKernel kernel)
        {
            _kernel = kernel;
            _lexer = new(text);
            _current = _lexer.Next();
        }

        public CadScriptResult Execute()
        {
            while (_current.Kind != TokenKind.End) ParseStatement();
            if (_result is null) throw new InvalidOperationException("Script must contain at least one 'solid name = ...;' statement.");
            return new CadScriptResult(_result, _parameters, _owned);
        }

        private void ParseStatement()
        {
            var kind = _current.Kind;
            if (kind is not (TokenKind.Let or TokenKind.Param or TokenKind.Solid))
                throw Error("Expected 'param', 'let', or 'solid'.");
            Next();
            var name = Expect(TokenKind.Identifier).Text;
            Expect(TokenKind.Equal);
            var value = ParseExpression();
            Expect(TokenKind.Semicolon);

            if (kind == TokenKind.Param)
            {
                var scalar = value.RequireScalar("param");
                _parameters[name] = scalar;
                _symbols[name] = value;
                return;
            }
            var solid = value.RequireSolid(kind == TokenKind.Solid ? "solid" : "let");
            _symbols[name] = Value.From(solid);
            if (kind == TokenKind.Solid) _result = solid;
        }

        private Value ParseExpression()
        {
            var left = ParsePrimary();
            while (_current.Kind is TokenKind.Plus or TokenKind.Minus or TokenKind.Ampersand)
            {
                var op = _current.Kind;
                Next();
                var right = ParsePrimary();
                var a = left.RequireSolid("boolean operator");
                var b = right.RequireSolid("boolean operator");
                left = Value.From(Own(op switch
                {
                    TokenKind.Plus => _kernel.Union(a, b),
                    TokenKind.Minus => _kernel.Difference(a, b),
                    _ => _kernel.Intersection(a, b)
                }));
            }
            return left;
        }

        private Value ParsePrimary()
        {
            if (_current.Kind == TokenKind.Number)
            {
                var n = _current.Number; Next(); return Value.From(n);
            }
            if (_current.Kind == TokenKind.Minus)
            {
                Next(); return Value.From(-ParsePrimary().RequireScalar("unary minus"));
            }
            if (_current.Kind == TokenKind.LParen)
            {
                Next(); var value = ParseExpression(); Expect(TokenKind.RParen); return value;
            }
            if (_current.Kind != TokenKind.Identifier) throw Error("Expected number, identifier, or function call.");

            var id = _current.Text;
            Next();
            if (_current.Kind != TokenKind.LParen)
            {
                if (!_symbols.TryGetValue(id, out var value)) throw Error($"Unknown symbol '{id}'.");
                return value;
            }

            Next();
            var args = new List<Value>();
            if (_current.Kind != TokenKind.RParen)
            {
                do
                {
                    args.Add(ParseExpression());
                    if (_current.Kind != TokenKind.Comma) break;
                    Next();
                } while (true);
            }
            Expect(TokenKind.RParen);
            return Invoke(id, args);
        }

        private Value Invoke(string name, IReadOnlyList<Value> args)
        {
            double S(int i) => args[i].RequireScalar(name);
            ISolid G(int i) => args[i].RequireSolid(name);
            void Count(int n)
            {
                if (args.Count != n) throw Error($"{name} expects {n} arguments, got {args.Count}.");
            }

            return name switch
            {
                "box" => Box(),
                "sphere" => Sphere(),
                "cylinder" => Cylinder(),
                "translate" => Transform3(_kernel.Translate),
                "rotate" => Transform3(_kernel.RotateDegrees),
                "scale" => Transform3(_kernel.Scale),
                "union" => Binary(_kernel.Union),
                "difference" => Binary(_kernel.Difference),
                "intersection" => Binary(_kernel.Intersection),
                _ => throw Error($"Unknown function '{name}'.")
            };

            Value Box() { Count(3); return Value.From(Own(_kernel.Box(S(0), S(1), S(2), true))); }
            Value Sphere() { Count(1); return Value.From(Own(_kernel.Sphere(S(0)))); }
            Value Cylinder() { Count(2); return Value.From(Own(_kernel.Cylinder(S(0), S(1)))); }
            Value Transform3(Func<ISolid, double, double, double, ISolid> f)
            {
                Count(4); return Value.From(Own(f(G(0), S(1), S(2), S(3))));
            }
            Value Binary(Func<ISolid, ISolid, ISolid> f)
            {
                Count(2); return Value.From(Own(f(G(0), G(1))));
            }
        }

        private T Own<T>(T value) where T : IDisposable { _owned.Add(value); return value; }
        private Token Expect(TokenKind kind)
        {
            if (_current.Kind != kind) throw Error($"Expected {kind}, got {_current.Kind} ('{_current.Text}').");
            var result = _current; Next(); return result;
        }
        private void Next() => _current = _lexer.Next();
        private FormatException Error(string message) => new($"CAD script parse error near '{_current.Text}': {message}");
    }
}
