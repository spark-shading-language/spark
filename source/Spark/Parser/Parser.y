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
%using Spark.AbstractSyntax

%YYLTYPE SourceRange

%{

    public Parser(AbstractScanner<ValueType, SourceRange> scanner)
        : base(scanner)
    {
    }

    public Spark.Parser.Generated.Scanner frogScanner {
        get { return (Spark.Parser.Generated.Scanner) this.Scanner; }
    }

    public AbsSyntaxInfo info(
	    SourceRange range )
    {
        return frogScanner.info(range);
    }

	public void Tag(
		SourceRange range,
		TokenType type )
	{
	    frogScanner.Tag(range, type);
	}
    
    public IdentifierFactory identifierFactory
    {
        get { return frogScanner.identifierFactory; }
    }

    private Identifier Op(
        string operatorName )
    {
        return identifierFactory.operatorIdentifier( operatorName );
    }

    private AbsTerm BinOp(
	    AbsSyntaxInfo info,
		AbsTerm left,
		Identifier op,
		AbsTerm right )
	{
	    return new AbsApp(info,
			new AbsVarRef(info, op), // \todo: tighter token range on the op?
			new AbsArg[]{
				new AbsPositionalArg(left.Info, left),
				new AbsPositionalArg(right.Info, right) } );
	}

    private AbsTerm BinOp(
	    AbsSyntaxInfo info,
		AbsTerm left,
		string op,
		AbsTerm right )
	{
	    return BinOp(info, left, Op(op), right);
	}

    private AbsTerm UnOp(
	    AbsSyntaxInfo info,
		Identifier op,
		AbsTerm arg )
	{
	    return new AbsApp(info,
			new AbsVarRef(info, op), // \todo: tighter token range on the op?
			new AbsArg[]{
				new AbsPositionalArg(arg.Info, arg) } );
	}

    private AbsTerm UnOp(
	    AbsSyntaxInfo info,
		string op,
		AbsTerm arg )
	{
	    return UnOp(info, Op(op), arg);
	}

    private AbsSourceRecord _result = null;

    public AbsSourceRecord result { get { return _result; } }

%}

%union { public Int64 intVal;
         public Double floatVal;
         public string stringVal;
         public AbsSourceRecord sourceRecordVal;
         public List<AbsGlobalDecl> globalDeclListVal;
         public AbsGlobalDecl globalDeclVal;
         public Identifier identifierVal;
         public List<AbsMemberDecl> memberDeclListVal;
         public AbsMemberDecl memberDeclVal;
         public List<AbsTerm> termListVal;
         public AbsTerm termVal;
         public List<AbsArg> argListVal;
         public AbsArg argVal;
         public List<AbsParamDecl> paramListVal;
         public AbsParamDecl paramVal;
         public List<AbsStmt> stmtListVal;
         public AbsStmt stmtVal;
         public List<AbsGenericParamDecl> genericParamListVal;
         public AbsGenericParamDecl genericParamVal;
         public AbsAttribute attributeVal;
         public AbsCase caseVal;
         public List<AbsCase> caseListVal;
		 public AbsConceptDecl conceptDeclVal; }

%token                      TOK_ABSTRACT
%token                      TOK_CASE
%token                      TOK_CLASS
%token                      TOK_CONCEPT
%token                      TOK_CONCRETE
%token                      TOK_ELEMENT
%token                      TOK_EXTENDS
%token                      TOK_FALSE
%token                      TOK_FINAL
%token                      TOK_FOR
%token                      TOK_IF
%token                      TOK_IMPLICIT
%token                      TOK_IN
%token                      TOK_INPUT
%token                      TOK_LET
%token                      TOK_METHOD
%token                      TOK_MIXIN
%token                      TOK_NAMESPACE
%token                      TOK_NEW
%token                      TOK_OPERATOR
%token                      TOK_OPTIONAL
%token                      TOK_OUTPUT
%token                      TOK_OVERRIDE
%token                      TOK_PRIMARY
%token                      TOK_RETURN
%token                      TOK_RETURNS
%token                      TOK_SEALED
%token                      TOK_SHADER
%token                      TOK_STRUCT
%token                      TOK_SWITCH
%token                      TOK_TRUE
%token                      TOK_TYPE
%token                      TOK_USING
%token                      TOK_VAR
%token                      TOK_VIRTUAL
%token                      TOK_VOID
%token                      TOK_WHERE

%token<floatVal>            TOK_FLOAT_LITERAL
%token<stringVal>           TOK_IDENTIFIER
%token<intVal>              TOK_INTEGER_LITERAL
%token<stringVal>           TOK_STRING_LITERAL

%token                      TOK_EQUALEQUAL
%token                      TOK_GREATEREQUAL
%token                      TOK_LESSEQUAL
%token                      TOK_NOTEQUAL
%token                      TOK_SHIFT_LEFT
%token                      TOK_SHIFT_RIGHT
%token                      TOK_ADDEQUAL
%token                      TOK_SUBEQUAL
%token                      TOK_MULEQUAL
%token                      TOK_DIVEQUAL
%token                      TOK_ANDEQUAL
%token                      TOK_OREQUAL

%token                      TOK_SCOPE

%type<sourceRecordVal>      source_record
%type<globalDeclListVal>    opt_global_decls
%type<globalDeclVal>        global_decl pipeline_decl
%type<memberDeclVal>        type_slot_decl struct_decl
%type<memberDeclVal>        element_decl
%type<genericParamListVal>  opt_generic_param_list
%type<genericParamListVal>  generic_param_list
%type<genericParamVal>      generic_param
%type<memberDeclListVal>    opt_member_decls
%type<memberDeclVal>        member_decl property_decl method_decl
%type<paramListVal>         opt_param_list param_list
%type<paramVal>             param
%type<termListVal>          opt_extends type_exp_list
%type<argListVal>           opt_arg_list arg_list
%type<argVal>               arg type_arg
%type<termVal>              type_exp opt_init exp
%type<argListVal>           type_arg_list
%type<stmtVal>              opt_method_body stmt block_stmt stmts
%type<stringVal>            string_literal
%type<identifierVal>        identifier
%type<attributeVal>         attribute attribute_body
%type<conceptDeclVal>       concept_decl

%type<termVal>              assignment_exp conditional_exp logical_or_exp
%type<termVal>              logical_and_exp inclusive_or_exp exclusive_or_exp
%type<termVal>              and_exp equality_exp relational_exp shift_exp
%type<termVal>              additive_exp multiplicative_exp cast_exp
%type<termVal>              unary_exp paren_exp postfix_exp primary_exp literal_exp var_ref
%type<identifierVal>        equality_op relational_op shift_op
%type<identifierVal>        additive_op multiplicative_op unary_op compound_op

%type<caseVal>              switch_case
%type<caseListVal>          switch_cases

%%

start
  : source_record
    { _result = $1; }
  ;

source_record
  : opt_global_decls
    { $$ = new AbsSourceRecord(info(@$), $1); }
  ;

opt_global_decls
  : /* empty */
    { $$ = new List<AbsGlobalDecl>(); }
  | opt_global_decls global_decl
    { $1.Add($2); $$ = $1; }
  ;

global_decl
  : pipeline_decl
    { $$ = $1; }
  ;

pipeline_decl
  : TOK_SHADER TOK_CLASS identifier opt_extends '{' opt_member_decls '}'
    { $$ = new AbsPipelineDecl(info(@$), $3, $4, $6); }
  | TOK_ABSTRACT pipeline_decl
    { $$ = $2; $$.Modifiers |= AbsModifiers.Abstract; }
  | TOK_MIXIN pipeline_decl
    { $$ = $2; $$.Modifiers |= AbsModifiers.Mixin; }
  | TOK_PRIMARY pipeline_decl
    { $$ = $2; $$.Modifiers |= AbsModifiers.Primary; }
  ;


type_slot_decl
  : TOK_TYPE identifier opt_generic_param_list ';'
    {
	    var result = new AbsTypeSlotDecl(info(@$), $2);
		$$ = result;
		if( $3 != null )
			result.GenericParams = $3;
	}
  ;

opt_generic_param_list
  : /* empty */
    { $$ = null; }
  | '[' generic_param_list ']'
    { $$ = $2; }
  | '[' generic_param_list error ']'
    { $$ = $2; }
  | '[' error ']'
    { $$ = null; }
  ;

generic_param_list
  : generic_param
    { $$ = new List<AbsGenericParamDecl>{ $1 }; }
  | generic_param_list ',' generic_param
    { $$ = $1; $$.Add($3); }
  ;

generic_param
  : TOK_TYPE identifier
    { $$ = new AbsGenericTypeParamDecl(info(@$), $2); }
  | type_exp identifier
    { $$ = new AbsGenericValueParamDecl(info(@$), $1, $2, false); }
  | TOK_IMPLICIT type_exp identifier
    { $$ = new AbsGenericValueParamDecl(info(@$), $2, $3, true); }
  | TOK_IMPLICIT type_exp
    { $$ = new AbsGenericValueParamDecl(info(@$), $2, identifierFactory.unique("_"), true); }
  ;

opt_extends
  : /* empty */
    { $$ = new List<AbsTerm>(); }
  | TOK_EXTENDS type_exp_list
    { $$ = $2; }
  ;

opt_member_decls
  : /* empty */
    { $$ = new List<AbsMemberDecl>(); }
  | opt_member_decls member_decl
    { $1.Add($2); $$ = $1; }
  ;

member_decl
  : element_decl
    { $$ = $1; }
  | property_decl
    { $$ = $1; }
  | method_decl
    { $$ = $1; }
  | concept_decl
    { $$ = $1; }
  | type_slot_decl
    { $$ = $1; }
  | struct_decl
    { $$ = $1; }
  | TOK_INPUT member_decl
    { $$ = $2; $$.Modifiers  |= AbsModifiers.Input; }
  | TOK_ABSTRACT member_decl
    { $$ = $2; $$.Modifiers |= AbsModifiers.Abstract; }
  | TOK_VIRTUAL member_decl
    { $$ = $2; $$.Modifiers |= AbsModifiers.Virtual; }
  | TOK_OVERRIDE member_decl
    { $$ = $2; $$.Modifiers |= AbsModifiers.Override; }
  | TOK_NEW member_decl
    { $$ = $2; $$.Modifiers |= AbsModifiers.New; }
  | attribute member_decl
    { $$ = $2; $$.Attributes.Add( $1 ); }
  | TOK_IMPLICIT member_decl
    { $$ = $2; $$.Modifiers  |= AbsModifiers.Implicit; }
  | TOK_CONCRETE member_decl
    { $$ = $2; $$.Modifiers  |= AbsModifiers.Concrete; }
  | TOK_OUTPUT member_decl
    { $$ = $2; $$.Modifiers  |= AbsModifiers.Output; }
  | TOK_OPTIONAL member_decl
    { $$ = $2; $$.Modifiers  |= AbsModifiers.Optional; }
  ;

attribute
  : '[' '[' attribute_body ']' ']'
    { $$ = $3; }
  ;

attribute_body
  : identifier
    { $$ = new AbsAttribute(info(@$), $1); }
  | identifier '(' opt_arg_list ')'
    { $$ = new AbsAttribute(info(@$), $1, $3); }
  ;

element_decl
  : TOK_ELEMENT identifier ';'
    { $$ = new AbsElementDecl(info(@$), $2); }
  ;

property_decl
  : type_exp identifier opt_init ';'
    { $$ = new AbsSlotDecl(info(@$), $2, $1, $3); }
  | identifier opt_init ';'
    { $$ = new AbsSlotDecl(info(@$), $1, null, $2); }
  ;

opt_init
  : /* empty */
    { $$ = null; }
  | '=' exp
    { $$ = $2; }
  | compound_op exp
    { $$ = BinOp( info(@$), new AbsBaseExp(info(@$)), $1, $2 ); }
  ;

compound_op
  : TOK_ADDEQUAL    { $$ = Op("+"); }
  | TOK_SUBEQUAL    { $$ = Op("-"); }
  | TOK_MULEQUAL    { $$ = Op("*"); }
  | TOK_DIVEQUAL    { $$ = Op("/"); }
  | TOK_ANDEQUAL    { $$ = Op("&"); }
  | TOK_OREQUAL     { $$ = Op("|"); }
  ;

concept_decl
  : TOK_CONCEPT identifier opt_generic_param_list '{' opt_member_decls '}'
    { $$ = new AbsConceptDecl(info(@$), $2, $3, $5); }
  ;

method_decl
  : type_exp identifier opt_generic_param_list '(' opt_param_list ')' opt_method_body
    {
		var result = new AbsMethodDecl(info(@$), $2, $1, $5, $7);
		$$ = result;
		if( $3 != null )
			result.GenericParams = $3;
    }
  ;

opt_param_list
  : /* empty */
    { $$ = new List<AbsParamDecl>(); }
  | param_list
  ;

param_list
  : param
    { $$ = new List<AbsParamDecl>{ $1 }; }
  | param_list ',' param
    { $$ = $1; $$.Add($3); }
  ;

param
  : type_exp identifier
    { $$ = new AbsParamDecl(info(@$), $2, $1); }
  ;

opt_method_body
  : ';'
    { $$ = null; }
  | block_stmt
  ;

type_exp_list
  : type_exp
    { $$ = new List<AbsTerm>{ $1 }; }
  | type_exp_list ',' type_exp
    { $1.Add($3); $$ = $1; }
  ;

type_exp
  : postfix_exp
    { $$ = $1; }
  | '@' var_ref postfix_exp
    {
        Tag(@1.Merge(@2), TokenType.Frequency);
        $$ = new AbsFreqQualTerm(info(@$), $2, $3);
    }
  ;

type_arg_list
  : type_arg
    { $$ = new List<AbsArg>(); $$.Add($1); }
  | type_arg_list ',' type_arg
    { $$ = $1; $$.Add($3); }
  ;

type_arg
  : type_exp
    { $$ = new AbsPositionalArg(info(@$), $1); }
  ;

struct_decl
  : TOK_STRUCT identifier '{' opt_member_decls '}'
    { $$ = new AbsStructDecl(info(@$), $2, $4); }
  ;

stmts
  : /* empty */
    { $$ = new AbsEmptyStmt(info(@$)); }
  | stmts stmt
    { $$ = new AbsSeqStmt(info(@$), $1, $2); }
  ;

stmt
  : block_stmt
  | ';'
    { $$ = new AbsEmptyStmt(info(@$)); }
  | exp ';'
    { $$ = new AbsExpStmt(info(@$), $1); }
  | TOK_RETURN ';'
    { $$ = new AbsReturnStmt(info(@$)); }
  | TOK_RETURN exp ';'
    { $$ = new AbsReturnStmt(info(@$), $2); }
  | type_exp identifier '=' exp ';'
    { $$ = new AbsLetStmt(info(@$), AbsLetFlavor.Value, $1, $2, $4); }
  | TOK_LET identifier '=' exp ';'
    { $$ = new AbsLetStmt(info(@$), AbsLetFlavor.Value, null, $2, $4); }
  | TOK_VAR identifier '=' exp ';'
    { $$ = new AbsLetStmt(info(@$), AbsLetFlavor.Variable, null, $2, $4); }
  | TOK_VAR type_exp identifier '=' exp ';'
    { $$ = new AbsLetStmt(info(@$), AbsLetFlavor.Variable, $2, $3, $5); }
  | TOK_SWITCH '(' exp ')' '{' switch_cases '}'
    { $$ = new AbsSwitchStmt(info(@$), $3, $6); }
  | TOK_IF '(' exp ')' stmt
    { $$ = new AbsIfStmt(info(@$), $3, $5, null); }
  | TOK_FOR '(' identifier TOK_IN exp ')' stmt
    { $$ = new AbsForStmt(info(@$), $3, $5, $7); }
  ;

switch_cases 
  : /* empty */
    { $$ = new List<AbsCase>(); }
  | switch_cases switch_case
    { $$ = $1; $$.Add($2); }
  ;

switch_case
  : TOK_CASE exp ':' stmt
    { $$ = new AbsCase(info(@$), $2, $4); }
  ;

block_stmt
  : '{' stmts '}'
    { $$ = $2; }
  ;

opt_arg_list
  : /* empty */
    { $$ = new List<AbsArg>(); }
  | arg_list
    { $$ = $1; }
  | arg_list ','
    { $$ = $1; }
  ;

arg_list
  : arg
    { $$ = new List<AbsArg>{ $1 }; }
  | arg_list ',' arg
    { $$ = $1; $$.Add($3); }
  ;

arg
  : exp
    { $$ = new AbsPositionalArg( info(@$), $1); }
  | identifier ':' exp
    { $$ = new AbsKeywordArg( info(@$), $1, $3); }
  ;

exp
  : assignment_exp
    { $$ = $1; }
  ;

assignment_exp
  : conditional_exp
  | unary_exp '=' assignment_exp
    { $$ = new AbsAssign( info(@$), $1, $3); }
  | unary_exp '@' assignment_exp
    { $$ = new AbsApp(info(@$), $1, new AbsArg[]{ new AbsPositionalArg(info(@3), $3) } ); }
/* \todo: compound assignment...
  | unary_expression assignment_op assignment_expression
*/
  ;

/*
assignment_op
  : '='
  | MUL_ASSIGN
  | DIV_ASSIGN
  | MOD_ASSIGN
  | ADD_ASSIGN
  | SUB_ASSIGN
  | LEFT_ASSIGN
  | RIGHT_ASSIGN
  | AND_ASSIGN
  | XOR_ASSIGN
  | OR_ASSIGN
  ;
*/

conditional_exp
  : logical_or_exp
  | logical_or_exp '?' exp ':' conditional_exp
    { $$ = new AbsIfTerm( info(@$), $1, $3, $5 ); }
  ;

logical_or_exp
  : logical_and_exp
/*
  | logical_or_exp TOK_OROR logical_and_exp
*/
  ;

logical_and_exp
  : inclusive_or_exp
/*
  | logical_and_exp TOK_ANDAND inclusive_or_exp
*/
  ;

inclusive_or_exp
  : exclusive_or_exp
  | inclusive_or_exp '|' exclusive_or_exp
    { $$ = BinOp( info(@$), $1, "|", $3 ); }
  ;

exclusive_or_exp
  : and_exp
  | exclusive_or_exp '^' and_exp
    { $$ = BinOp( info(@$), $1, "^", $3 ); }
  ;

and_exp
  : equality_exp
  | and_exp '&' equality_exp
    { $$ = BinOp( info(@$), $1, "&", $3 ); }
  ;

equality_exp
  : relational_exp
  | equality_exp equality_op relational_exp
    { $$ = BinOp( info(@$), $1, $2, $3 ); }
  ;

equality_op
  : TOK_EQUALEQUAL
    { $$ = Op("=="); }
  | TOK_NOTEQUAL
    { $$ = Op("!="); }
  ;

relational_exp
  : shift_exp
  | relational_exp relational_op shift_exp
    { $$ = BinOp( info(@$), $1, $2, $3 ); }
  ;

relational_op
  : '<'
    { $$ = Op("<"); }
  | '>'
    { $$ = Op(">"); }
  | TOK_LESSEQUAL
    { $$ = Op("<="); }
  | TOK_GREATEREQUAL
    { $$ = Op(">="); }
  ;

shift_exp
  : additive_exp
  | shift_exp shift_op additive_exp
    { $$ = BinOp( info(@$), $1, $2, $3 ); }
  ;

shift_op
  : TOK_SHIFT_LEFT
    { $$ = Op("<<"); }
  | TOK_SHIFT_RIGHT
    { $$ = Op(">>"); }
  ;

additive_exp
  : multiplicative_exp
  | additive_exp additive_op multiplicative_exp
    { $$ = BinOp( info(@$), $1, $2, $3 ); }
  ;

additive_op
  : '+'
    { $$ = Op("+"); }
  | '-'
    { $$ = Op("-"); }
  ;

multiplicative_exp
  : cast_exp
  | multiplicative_exp multiplicative_op cast_exp
    { $$ = BinOp( info(@$), $1, $2, $3 ); }
  ;

multiplicative_op
  : '*'
    { $$ = Op("*"); }
  | '/'
    { $$ = Op("/"); }
  | '%'
    { $$ = Op("%"); }
  ;

cast_exp
  : unary_exp
/*
  | '(' exp ')' cast_exp
    { $$ = new AbsApp( info(@$), $2, new AbsArg[]{ new AbsPositionalArg(info(@4), $4) } ); }
*/
  ;

unary_exp
  : paren_exp
  | unary_op cast_exp
    { $$ = UnOp( info(@$), $1, $2 ); }
  ;

unary_op
  : '-'
    { $$ = Op("-"); }
  | '~'
    { $$ = Op("~"); }
  | '!'
    { $$ = Op("!"); }
  ;

paren_exp
  : postfix_exp
    { $$ = $1; }
  | '(' exp ')'
    { $$ = $2; }
  ;

postfix_exp
  : primary_exp
  | postfix_exp '(' opt_arg_list ')'
    { $$ = new AbsApp(info(@$), $1, $3); }
  | '@' var_ref '(' opt_arg_list ')'
    { $$ = new AbsApp(info(@$), $2, $4); }
  | postfix_exp '[' type_arg_list ']'
    { $$ = new AbsGenericApp(info(@$), $1, $3); }
  | postfix_exp '.' identifier
    { $$ = new AbsMemberRef(info(@$), $1, $3); }
  ;

primary_exp
  : var_ref
    { $$ = $1; }
  | literal_exp
    { $$ = $1; }
  ;

var_ref
  : identifier
    { $$ = new AbsVarRef(info(@$), $1); }
  ;

literal_exp
  : TOK_INTEGER_LITERAL
    { $$ = new AbsLit<Int32>(info(@$), (Int32) $1); }
  | TOK_FLOAT_LITERAL
    { $$ = new AbsLit<Double>(info(@$), $1); }
  | string_literal
    { $$ = new AbsLit<String>(info(@$), $1); }
  | TOK_VOID
    { $$ = new AbsVoid(info(@$)); }
  | TOK_TRUE
    { $$ = new AbsLit<bool>(info(@$), true); }
  | TOK_FALSE
    { $$ = new AbsLit<bool>(info(@$), false); }
  ;

string_literal
  : TOK_STRING_LITERAL
  ;

identifier
  : TOK_IDENTIFIER
    { $$ = identifierFactory.simpleIdentifier($1); }
  | TOK_OPERATOR '+'
    { $$ = identifierFactory.operatorIdentifier("+"); }
  | TOK_OPERATOR '-'
    { $$ = identifierFactory.operatorIdentifier("-"); }
  | TOK_OPERATOR '*'
    { $$ = identifierFactory.operatorIdentifier("*"); }
  | TOK_OPERATOR '/'
    { $$ = identifierFactory.operatorIdentifier("/"); }
  | TOK_OPERATOR '%'
    { $$ = identifierFactory.operatorIdentifier("%"); }
  | TOK_OPERATOR '<'
    { $$ = identifierFactory.operatorIdentifier("<"); }
  | TOK_OPERATOR '>'
    { $$ = identifierFactory.operatorIdentifier(">"); }
  | TOK_OPERATOR TOK_LESSEQUAL
    { $$ = identifierFactory.operatorIdentifier("<="); }
  | TOK_OPERATOR TOK_GREATEREQUAL
    { $$ = identifierFactory.operatorIdentifier(">="); }
  | TOK_OPERATOR TOK_SHIFT_LEFT
    { $$ = identifierFactory.operatorIdentifier("<<"); }
  | TOK_OPERATOR TOK_SHIFT_RIGHT
    { $$ = identifierFactory.operatorIdentifier(">>"); }
  | TOK_OPERATOR TOK_EQUALEQUAL
    { $$ = identifierFactory.operatorIdentifier("=="); }
  | TOK_OPERATOR TOK_NOTEQUAL
    { $$ = identifierFactory.operatorIdentifier("!="); }
  | TOK_OPERATOR '&'
    { $$ = identifierFactory.operatorIdentifier("&"); }
  | TOK_OPERATOR '|'
    { $$ = identifierFactory.operatorIdentifier("|"); }
  | TOK_OPERATOR '!'
    { $$ = identifierFactory.operatorIdentifier("!"); }
  | TOK_OPERATOR '(' identifier ')'
    { $$ = identifierFactory.operatorIdentifier(($3).ToString()); }
  | TOK_OPERATOR '(' ')'
    { $$ = identifierFactory.operatorIdentifier("()"); }
  ;

%%
