grammar Zenkai;

// A Zenkai script is a flat sequence of opcode calls and labels.
// Every opcode call `Name(args...)` assembles to: push each arg (PushByte/
// PushVarint/PushMultiVarint, chosen by the assembler), then Step(opcodeIndex).
// Jump/JumpIfFalse/LoopOrJump are special-cased: they are main-dispatch-table
// instructions (not Step-table opcodes) that take a LABEL, not an integer -
// the assembler resolves the label to the correct signed-byte relative delta.
// See DBZKit/BytecodeVM_OpCodes.md and Legacy.wiki/Zenkai for the full opcode
// reference this grammar's names/arities come from.

script : stmt* EOF ;

stmt
    : label
    | call
    ;

label : IDENT ':' ;

call : IDENT '(' argList? ')' ';'? ;

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
