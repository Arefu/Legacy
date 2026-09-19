grammar Zenkai;

// A Zenkai script is a flat sequence of opcode calls and labels.
// Every opcode call `Name(args...)` assembles to: push each arg (PushByte/
// PushVarint/PushMultiVarint, chosen by the assembler), then Step(opcodeIndex).
// Jump/JumpIfFalse/LoopOrJump are special-cased: they are main-dispatch-table
// instructions (not Step-table opcodes) that take a LABEL, not an integer -
// the assembler resolves the label to the correct signed-byte relative delta.
// See DBZKit/BytecodeVM_OpCodes.md and Legacy.wiki/Zenkai for the full opcode
// reference this grammar's names/arities come from.

// var/if-else are a SOURCE-LEVEL convenience layer, not real VM features -- the
// bytecode VM has no addressable local storage at all (confirmed via IDA 2026-09-19:
// the only candidate mechanism, PushToAltStack, turns out to share memory with
// LoopOrJump's loop-nesting-depth counter, so it's loop bookkeeping, not a scratch
// slot). `var X = Call(...);` therefore does NOT reserve a VM stack/memory slot --
// it just remembers Call(...) under the name X, and every later reference to X
// re-emits Call(...)'s bytecode in place (see ZenkaiAssembler). That's safe for
// pure "get a value" opcodes (re-running a flag test or item-count read is a no-op
// either way) but NOT for StackRand/StackRandChance, which the compiler refuses to
// let you reference more than once for exactly that reason -- a second reference
// would silently be a NEW random roll, not the value you already branched on.
// `if`/`else` compiles to the same Jump/JumpIfFalse the VM already has; there is no
// new control-flow primitive here either, just auto-generated labels.

script : stmt* EOF ;

stmt
    : label
    | call
    | varDecl
    | ifStmt
    | returnStmt
    ;

// `return 2;` / `return someVar;` -- ends the script leaving that value on the VM stack
// as the script's result (what the caller reads: quest conditions, dialog scripts, ...).
// Compiles to: push the value, then END (0x11). Reserved word, so it can't be a var/label
// name. The ';' is optional, like everywhere else. A bare `return` followed by a new
// statement is disambiguated by lookahead: an IDENT only counts as the returned var if it
// isn't the start of a call (`Foo(`) or a label (`L1:`). To return a computed value, just
// end the script with the call itself (`StackTestStoryFlag(191);`) -- its result is
// already the script result.
returnStmt : 'return' (INT | {_input.Lt(2).Text != "(" && _input.Lt(2).Text != ":"}? IDENT)? ';'? ;

label : IDENT ':' ;

call : IDENT '(' argList? ')' ';'? ;

varDecl : 'var' IDENT '=' call ;

ifStmt : 'if' '(' cond ')' body ('else' body)? ;

// Braces are optional for a single-statement body, same as C# -- `if (cond) Foo();`
// is exactly equivalent to `if (cond) { Foo(); }`. A bare body can itself be a label
// or a nested ifStmt (again matching C#'s `if (a) if (b) Foo(); else Bar();`), not
// just a plain call/varDecl.
body : block | stmt ;

block : '{' stmt* '}' ;

// Comparisons compile straight to the VM's own raw comparison instructions
// (0x0B-0x10 in the main dispatch table: StackCmpEq/Ne/Gt/Ge/Lt/Le) -- these
// already exist in every real ROM script's bytecode, this just gives them
// readable syntax instead of leaving them as the disassembler's raw
// "// raw main-dispatch opcode 0x0D" comments. LHS/RHS can each be a literal,
// a var reference, or an inline call, same as everywhere else.
cond
    : IDENT                    # condVar
    | call                     # condCall
    | condTerm CMPOP condTerm  # condCompare
    ;

condTerm
    : INT     # termInt
    | IDENT   # termVar
    | call    # termCall
    ;

CMPOP : '==' | '!=' | '>=' | '<=' | '>' | '<' ;

argList : arg (',' arg)* ;

arg
    : INT     # intArg
    | IDENT   # labelArg
    ;

INT
    : '-'? '0x' [0-9a-fA-F]+
    | '-'? [0-9]+
    ;

IDENT : [a-zA-Z_][a-zA-Z0-9_]* ;

LINE_COMMENT : '//' ~[\r\n]* -> skip ;
WS : [ \t\r\n]+ -> skip ;
