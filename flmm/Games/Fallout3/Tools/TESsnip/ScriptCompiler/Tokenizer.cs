using System;
using System.Collections.Generic;
using System.Text;

namespace Fomm.Games.Fallout3.Tools.TESsnip.ScriptCompiler
{
  internal enum TokenType
  {
    Unknown,
    Integer,
    Float,
    Keyword,
    Symbol,
    Local,
    Global,
    Function,
    edid,
    Null
  }

  internal enum Keywords
  {
    If,
    ElseIf,
    Else,
    EndIf,
    ScriptName,
    Scn,
    Short,
    Int,
    Float,
    Ref,
    Begin,
    End,
    Set,
    To,
    Return,
    ShowMessage,
    NotAKeyword
  }

  internal struct Token
  {
    public static readonly Token Null = new Token(TokenType.Null, null);
    public static readonly Token NewLine = new Token(TokenType.Symbol, "\n");

    public readonly TokenType type;
    public readonly string token;
    public readonly string utoken;
    public readonly Keywords keyword;

    private static readonly Keywords[] typelist =
    {
      Keywords.Int, Keywords.Float, Keywords.Ref
    };

    private static readonly Keywords[] flowlist =
    {
      Keywords.If, Keywords.ElseIf, Keywords.Else, Keywords.EndIf, Keywords.Return
    };

    public Token(TokenType type, string token)
    {
      this.type = type;
      utoken = token;
      this.token = token;
      keyword = Keywords.NotAKeyword;
    }

    public Token(TokenType type, string ltoken, string token)
    {
      this.type = type;
      utoken = token;
      this.token = ltoken;
      keyword = Keywords.NotAKeyword;
    }

    public Token(TokenType type, Keywords keyword)
    {
      if (keyword == Keywords.Short)
      {
        keyword = Keywords.Int;
      }
      else if (keyword == Keywords.Scn)
      {
        keyword = Keywords.ScriptName;
      }
      this.type = type;
      this.keyword = keyword;
      token = keyword.ToString();
      utoken = token;
    }

    public override string ToString()
    {
      if (type == TokenType.Keyword)
      {
        return keyword.ToString();
      }
      return token;
    }

    public bool IsFlowControl()
    {
      return type == TokenType.Keyword && Array.IndexOf(flowlist, keyword) != -1;
    }

    public bool IsType()
    {
      return type == TokenType.Keyword && Array.IndexOf(typelist, keyword) != -1;
    }

    public bool IsSymbol(string s)
    {
      return type == TokenType.Symbol && s == token;
    }

    public bool IsKeyword(Keywords k)
    {
      return type == TokenType.Keyword && keyword == k;
    }

    /*public bool IsLiteral() {
            if(type==TokenType.String||type==TokenType.Float||type==TokenType.Integer) return true;
            return false;
        }*/
  }

  internal class TokenStream
  {
    private static readonly string[] ReservedWords =
    {
      "if", "elseif", "else", "endif", "scriptname", "scn", "short", "int", "float", "ref", "begin", "end", "set", "to",
      "return", "showmessage"
    };

    private static readonly List<string> globalVars = new List<string>();
    private static readonly List<string> functions = new List<string>();
    private static readonly List<string> edids = new List<string>();
    private readonly List<string> localVars = new List<string>();

    public void AddLocal(string s)
    {
      localVars.Add(s);
    }

    public static void AddGlobal(string s)
    {
      globalVars.Add(s);
    }

    public static void AddFunction(string s)
    {
      functions.Add(s);
    }

    public static void AddEdid(string s)
    {
      edids.Add(s);
    }

    public static void Reset()
    {
      globalVars.Clear();
      edids.Clear();
    }

    private readonly Queue<char> input;
    private readonly Queue<Token> storedTokens;

    public int Line { get; private set; }

    private readonly List<string> errors;

    private void AddError(string msg)
    {
      errors.Add(Line + ": " + msg);
    }

    private void SkipLine()
    {
      while (input.Count > 0 && input.Dequeue() != '\n') {}
      Line++;
    }

    private char SafePop()
    {
      if (input.Count == 0)
      {
        return '\0';
      }
      var c = input.Dequeue();
      while (c == '\r')
      {
        if (input.Count == 0)
        {
          return '\0';
        }
        c = input.Dequeue();
      }
      if (c == '\t' || c == ',')
      {
        c = ' ';
      }
      if (c < 32 && c != '\n')
      {
        AddError("There is an invalid character in the file");
      }
      return c;
    }

    private char SafePeek()
    {
      if (input.Count == 0)
      {
        return '\0';
      }
      var c = input.Peek();
      while (c == '\r')
      {
        input.Dequeue();
        if (input.Count == 0)
        {
          return '\0';
        }
        c = input.Peek();
      }
      if (c == '\t' || c == ',')
      {
        c = ' ';
      }
      if (c < 32 && c != '\n')
      {
        AddError("There is an invalid character in the file");
      }
      return c;
    }

    private readonly StringBuilder builder = new StringBuilder(32);

    private static Token FromWord(string token)
    {
      int i;
      var ltoken = token.ToLowerInvariant();
      if (char.IsDigit(token[0]) || (token.Length > 1 && (token[0] == '.' || token[0] == '-') && char.IsDigit(token[1])))
      {
        if (token.Contains(".") || ltoken.Contains("e"))
        {
          return new Token(TokenType.Float, token);
        }
        return new Token(TokenType.Integer, token);
      }
      if ((i = Array.IndexOf(ReservedWords, ltoken)) != -1)
      {
        return new Token(TokenType.Keyword, (Keywords) i);
      }
      return new Token(TokenType.Unknown, ltoken, token);
    }

    private Token PopTokenInternal2()
    {
      while (true)
      {
        char c;
        while (true)
        {
          c = SafePop();
          if (c == '\0')
          {
            return Token.Null;
          }

          if (c == '\n')
          {
            Line++;
            return Token.NewLine;
          }

          if (c == ';')
          {
            SkipLine();
            return Token.NewLine;
          }

          if (!char.IsWhiteSpace(c))
          {
            break;
          }
        }
        if (char.IsLetterOrDigit(c) || c == '_' || ((c == '.' || c == '~') && char.IsDigit(SafePeek())))
        {
          builder.Length = 0;
          builder.Append(c == '~' ? '-' : c);
          var numeric = char.IsDigit(c);
          while (true)
          {
            c = SafePeek();
            if (char.IsLetterOrDigit(c) || c == '_' || (numeric && c == '.'))
            {
              builder.Append(input.Dequeue());
            }
            else
            {
              break;
            }
          }
          return FromWord(builder.ToString());
        }

        switch (c)
        {
          case '"':
            builder.Length = 0;
            while ((c = SafePop()) != '"')
            {
              if (c == '\r' || c == '\n' || c == '\0')
              {
                AddError("Unexpected end of line");
                break;
              }
              if (c == '\\')
              {
                switch (c = SafePop())
                {
                  case '\0':
                  case '\r':
                  case '\n':
                    AddError("Unexpected end of line");
                    return FromWord(builder.ToString());
                  case '\\':
                    builder.Append('\\');
                    break;
                  case 'n':
                    builder.Append('\n');
                    break;
                  case '"':
                    builder.Append('"');
                    break;
                  default:
                    AddError("Unrecognised escape sequence");
                    builder.Append(c);
                    break;
                }
              }
              else
              {
                builder.Append(c);
              }
            }
            return FromWord(builder.ToString());
          case '+':
            return new Token(TokenType.Symbol, "+");
          case '-':
            return new Token(TokenType.Symbol, "-");
          case '*':
            if (SafePeek() == '*')
            {
              input.Dequeue();
              return new Token(TokenType.Symbol, "**");
            }
            return new Token(TokenType.Symbol, "*");
          case '/':
            if (SafePeek() == '=')
            {
              input.Dequeue();
              return new Token(TokenType.Symbol, "/=");
            }
            if (SafePeek() == ')')
            {
              input.Dequeue();
              return new Token(TokenType.Symbol, "/)");
            }
            return new Token(TokenType.Symbol, "/");
          case '!':
            if (SafePeek() == '=')
            {
              input.Dequeue();
              return new Token(TokenType.Symbol, "!=");
            }
            AddError("Illegal symbol '!'");
            return new Token(TokenType.Symbol, "!");
          case '=':
            if (SafePeek() == '=')
            {
              input.Dequeue();
              return new Token(TokenType.Symbol, "==");
            }
            AddError("Illegal symbol '='");
            return new Token(TokenType.Symbol, "=");
          case '>':
            if (SafePeek() == '=')
            {
              input.Dequeue();
              return new Token(TokenType.Symbol, ">=");
            }
            return new Token(TokenType.Symbol, ">");
          case '<':
            if (SafePeek() == '=')
            {
              input.Dequeue();
              return new Token(TokenType.Symbol, "<=");
            }
            return new Token(TokenType.Symbol, "<");
          case '(':
            return new Token(TokenType.Symbol, "(");
          case ')':
            return new Token(TokenType.Symbol, ")");
            //case ',':
            //    return new Token(TokenType.Symbol, ",");
          case '&':
            if (SafePeek() == '&')
            {
              input.Dequeue();
              return new Token(TokenType.Symbol, "&&");
            }
            AddError("Illegal symbol '&'");
            return new Token(TokenType.Symbol, "&");
          case '|':
            if (SafePeek() == '|')
            {
              input.Dequeue();
              return new Token(TokenType.Symbol, "||");
            }
            AddError("Illegal symbol '|'");
            return new Token(TokenType.Symbol, "|");
          case '.':
            return new Token(TokenType.Symbol, ".");
          default:
            AddError("Unexpected character");
            SkipLine();
            break;
        }
      }
    }

    private void PopTokenInternal()
    {
      var t = PopTokenInternal2();
      storedTokens.Enqueue(t);
    }

    private Token DequeueToken()
    {
      if (storedTokens.Count == 0)
      {
        return Token.Null;
      }
      var t = storedTokens.Dequeue();
      if (t.type == TokenType.Unknown)
      {
        if (localVars.Contains(t.token))
        {
          return new Token(TokenType.Local, t.token, t.utoken);
        }
        if (globalVars.Contains(t.token))
        {
          return new Token(TokenType.Global, t.token, t.utoken);
        }
        if (functions.Contains(t.token))
        {
          return new Token(TokenType.Function, t.token, t.utoken);
        }
        if (edids.Contains(t.token))
        {
          return new Token(TokenType.edid, t.token, t.utoken);
        }
      }
      return t;
    }

    private Token[] lastTokens;
    private readonly List<Token> getNextStatementTokens = new List<Token>();

    public Token[] PopNextStatement()
    {
      if (lastTokens != null)
      {
        var tmp = lastTokens;
        lastTokens = null;
        return tmp;
      }
      Line++;
      var t = DequeueToken();
      while (t.IsSymbol("\n"))
      {
        Line++;
        t = DequeueToken();
      }
      if (storedTokens.Count == 0)
      {
        return new Token[0];
      }
      getNextStatementTokens.Clear();
      while (t.type != TokenType.Null && !t.IsSymbol("\n"))
      {
        getNextStatementTokens.Add(t);
        t = DequeueToken();
      }
      return getNextStatementTokens.ToArray();
    }

    public Token[] PeekNextStatement()
    {
      if (lastTokens == null)
      {
        lastTokens = PopNextStatement();
      }
      return lastTokens;
    }

    public TokenStream(string file, List<string> errors)
    {
      this.errors = errors;
      Line = 1;
      input = new Queue<char>(file.ToCharArray());
      input.Enqueue('\n');
      storedTokens = new Queue<Token>();
      while (input.Count > 0)
      {
        PopTokenInternal();
      }
      Line = 0;
    }
  }
}