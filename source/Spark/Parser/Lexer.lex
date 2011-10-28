// Copyright 2011 Intel Corporation
// All Rights Reserved
//
// Permission is granted to use, copy, distribute and prepare derivative works of this
// software for any purpose and without fee, provided, that the above copyright notice
// and this statement appear in all copies.  Intel makes no representations about the
// suitability of this software for any purpose.  THIS SOFTWARE IS PROVIDED "AS IS."
// INTEL SPECIFICALLY DISCLAIMS ALL WARRANTIES, EXPRESS OR IMPLIED, AND ALL LIABILITY,
// INCLUDING CONSEQUENTIAL AND OTHER INDIRECT DAMAGES, FOR THE USE OF THIS SOFTWARE,
// INCLUDING LIABILITY FOR INFRINGEMENT OF ANY PROPRIETARY RIGHTS, AND INCLUDING THE
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE.  Intel does not
// assume any responsibility for any errors which may appear in this software nor any
// responsibility to update it.

%namespace Spark.Parser.Generated

%using Spark;
%using Spark.AbstractSyntax;

%scanbasetype LexerBase

%{

    public Scanner(
        IDiagnosticsCollection errors,
        IdentifierFactory identifierFactory,
        string fileName )
    {
        _errors = errors;
        _identifierFactory = identifierFactory;
        _fileName = fileName;
    }

    public Scanner(
        System.IO.Stream stream,
        IDiagnosticsCollection errors,
        IdentifierFactory identifierFactory,
        string fileName )
        : this(errors, identifierFactory, fileName)
    {
        SetSource( stream );
    }

    public Scanner(
        string source,
        IDiagnosticsCollection errors,
        IdentifierFactory identifierFactory,
        string fileName )
        : this(errors, identifierFactory, fileName)
    {
        SetSource( source, 0 );
    }

    public IdentifierFactory identifierFactory
    {
        get { return _identifierFactory; }
    }

    private IdentifierFactory _identifierFactory;
    private string _fileName;

    public AbsSyntaxInfo info(
		SourceRange range )
    {
        return new AbsSyntaxInfo(range);
    }

	public void Tag(
	    SourceRange range,
		TokenType tokenType )
	{
		if( _tags != null )
			_tags.Tag( range, tokenType );
	}

	public SourceRange Range()
	{
		yylloc = new SourceRange(
			_fileName,
			new SourcePos(tokLin, tokCol, tokPos),
			new SourcePos(tokELin, tokECol, tokEPos));
		return yylloc;
	}

    public override void yyerror(string format, params object[] args)
    {
        _errors.Add(Severity.Error,
                    yylloc,
                    args.Length == 0 ? format : string.Format(format, args));
    }

	public ITagSink TagSink
	{
		get { return _tags; }
		set { _tags = value; }
	}

    private IDiagnosticsCollection _errors;
	private ITagSink _tags;

%}

DecDigit [0-9]
HexDigit [0-9a-fA-F]
OctDigit [0-7]
FloatExp e{DecDigit}+
FloatSuffix [fdFD]

DecNumber {DecDigit}+

IdInit [A-Za-z\_]
IdChar [A-Za-z0-9\_]

WhiteSpace [\t\r\n ]+

StringChar [^"]

%%

"abstract"          { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_ABSTRACT; }
"case"              { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_CASE; }
"class"             { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_CLASS; }
"concept"           { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_CONCEPT; }
"concrete"          { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_CONCRETE; }
"element"           { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_ELEMENT; }
"extends"           { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_EXTENDS; }
"false"             { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_FALSE; }
"final"             { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_FINAL; }
"for"               { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_FOR; }
"if"                { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_IF; }
"implicit"          { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_IMPLICIT; }
"in"                { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_IN; }
"input"             { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_INPUT; }
"let"               { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_LET; }
"method"            { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_METHOD; }
"mixin"             { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_MIXIN; }
"namespace"         { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_NAMESPACE; }
"new"               { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_NEW; }
"operator"          { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_OPERATOR; }
"__optional"        { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_OPTIONAL; }
"output"            { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_OUTPUT; }
"override"          { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_OVERRIDE; }
"primary"           { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_PRIMARY; }
"record"            { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_ELEMENT; }
"return"            { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_RETURN; }
"returns"           { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_RETURNS; }
"sealed"            { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_SEALED; }
"shader"            { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_SHADER; }
"struct"            { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_STRUCT; }
"switch"            { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_SWITCH; }
"true"              { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_TRUE; }
"type"              { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_TYPE; }
"using"             { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_USING; }
"var"               { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_VAR; }
"virtual"           { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_VIRTUAL; }
"void"              { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_VOID; }
"where"             { Tag(Range(), TokenType.Keyword); return (int)Tokens.TOK_WHERE; }

{IdInit}{IdChar}*   { yylval.stringVal = yytext; return (int)Tokens.TOK_IDENTIFIER; }

{DecDigit}*         { Tag(Range(), TokenType.Literal); yylval.intVal = Int64.Parse(yytext); return (int)Tokens.TOK_INTEGER_LITERAL; }
0x{HexDigit}+       { Tag(Range(), TokenType.Literal);
                      yylval.intVal = Int64.Parse(yytext.Substring(2),
                          System.Globalization.NumberStyles.HexNumber);
                      return (int)Tokens.TOK_INTEGER_LITERAL;
                    }

([\+])?{DecDigit}*\.{DecDigit}+{FloatExp}?{FloatSuffix}?    {
                                    Tag(Range(), TokenType.Literal);
                                    yylval.floatVal = Double.Parse(yytext.TrimEnd( new char[] { 'f', 'd', 'F', 'D' } ),
                                      System.Globalization.NumberStyles.Float);
                                    return (int) Tokens.TOK_FLOAT_LITERAL;
                                  }
([\+])?{DecDigit}+\.{DecDigit}*{FloatExp}?{FloatSuffix}?    {
                                    Tag(Range(), TokenType.Literal);
                                    yylval.floatVal = Double.Parse(yytext.TrimEnd( new char[] { 'f', 'd', 'F', 'D' } ),
                                      System.Globalization.NumberStyles.Float);
                                   return (int) Tokens.TOK_FLOAT_LITERAL;
                                 }

[\"]{StringChar}*\" { Tag(Range(), TokenType.Literal); yylval.stringVal = yytext.Substring(1,yytext.Length-2); return (int)Tokens.TOK_STRING_LITERAL; }

"//"[^\r\n]*[\r\n]      { Tag(Range(), TokenType.Comment); }

"/*"([^*]|[\r\n]|(\*+([^*/]|[\r\n])))*"*/"    { Tag(Range(), TokenType.Comment); }

"+"                 { return (int) '+'; }
"-"                 { return (int) '-'; }
"*"                 { return (int) '*'; }
"/"                 { return (int) '/'; }
"%"                 { return (int) '%'; }
"!"                 { return (int) '!'; }
"~"                 { return (int) '~'; }
"^"                 { return (int) '^'; }
"&"                 { return (int) '&'; }
"|"                 { return (int) '|'; }
"<<"                { return (int) Tokens.TOK_SHIFT_LEFT; }
">>"                { return (int) Tokens.TOK_SHIFT_RIGHT; }
"<="                { return (int) Tokens.TOK_LESSEQUAL; }
">="                { return (int) Tokens.TOK_GREATEREQUAL; }
"=="                { return (int) Tokens.TOK_EQUALEQUAL; }
"!="                { return (int) Tokens.TOK_NOTEQUAL; }
"+="                { return (int) Tokens.TOK_ADDEQUAL; }
"-="                { return (int) Tokens.TOK_SUBEQUAL; }
"*="                { return (int) Tokens.TOK_MULEQUAL; }
"/="                { return (int) Tokens.TOK_DIVEQUAL; }
"&="                { return (int) Tokens.TOK_ANDEQUAL; }
"|="                { return (int) Tokens.TOK_OREQUAL; }
"<"                 { return (int) '<'; }
">"                 { return (int) '>'; }
"@"                 { return (int) '@'; }

"="                 { return (int) '='; }
"."                 { return (int) '.'; }
","                 { return (int) ','; }
"("                 { return (int) '('; }
")"                 { return (int) ')'; }
"{"                 { return (int) '{'; }
"}"                 { return (int) '}'; }
"["                 { return (int) '['; }
"]"                 { return (int) ']'; }
":"                 { return (int) ':'; }
";"                 { return (int) ';'; }
"?"                 { return (int) '?'; }

" "                 {}
[\t]                {}
\r\n                {}
[\n]                {}
.                   {	Range();
						yyerror("Unexpected character '{0}'", yytext); }

%{
    Range();
%}

%%

