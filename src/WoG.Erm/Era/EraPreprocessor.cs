using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WoG.Erm.Syntax;

namespace WoG.Erm.Era;

/// <summary>
/// Port of Era's PreprocessErm (Erm.pas, Era 3.9.31): a text-to-text pass run on every script before the
/// classic ERM compiler sees it. It replaces
///  * (FuncName) by a function id (all scripts; ids from 95000, event names pre-registered);
///  * in ZVSE2 scripts also local variables (name:y), arrays (arr[5]:y), addresses (@name), freeing (-name),
///    constants (CONST), !#DC(CONST) = value; declarations, !#VA(...) dummy commands, (FILE) (LINE) (CODE);
///  * labels [:name] / [name] by command indexes (used by SN:G);
///  * "!!!" by "!" (disables a command).
/// The port keeps Era's algorithm step by step (including its quirks), so that the same input yields the
/// same output. Errors that Era shows in a message box are collected as diagnostics; like Era, processing
/// continues.
/// </summary>
public static class EraPreprocessor
{
    public const string Erm2Signature = "ZVSE2";

    public static bool IsErm2(string script) => script.Length > 5 && string.CompareOrdinal(script, 0, Erm2Signature, 0, 5) == 0;

    public static string Process(string scriptName, string script, EraNames names, List<ErmDiagnostic>? diagnostics = null)
    {
        var impl = new Impl(scriptName, script, names, diagnostics);
        return impl.Run();
    }

    sealed class LocalVar
    {
        public char VarType;
        public bool IsNegative;
        public int StartIndex;
        public int Count;

        /// <summary>Really compiled start index, not logical one.</summary>
        public int RealStartIndex => IsNegative ? -StartIndex - Count + 1 : StartIndex;
    }

    sealed class VarRange
    {
        public int StartIndex;
        public int Count;
        public VarRange? NextRange;
    }

    sealed class VarsPool
    {
        public int StartIndex;
        public int Count;
        public bool IsNegative;
        public VarRange? FreeRanges;
    }

    struct ParsedLocalVar
    {
        public string Name;
        public int Index;
        public char VarType;
        public bool IsFreeing;
        public bool IsDeclaration;
        public bool IsAddr;
        public bool HasIndex;
        public LocalVar? IndexVar;
    }

    enum Scope { Global, Cmd }
    enum CmdType { Instruction, Receiver, Trigger }

    sealed class Impl
    {
        const string MagicSizeConstName = "SIZE";
        const int MagicSizeConst = -1520028087;
        const int IdentTypeConst = 1, IdentTypeFunc = 2, IdentTypeVar = 3;
        const int NoLabel = -1;
        const int LocalY = 0, LocalX = 1, LocalZ = 2, LocalE = 3;

        readonly string scriptName;
        readonly EraNames names;
        readonly List<ErmDiagnostic>? diags;
        readonly Scanner sc;

        // TStrList with objects: Buf[i] text, BufObj[i] = previous unresolved label index
        readonly List<string> buf = new();
        readonly List<int> bufObj = new();
        readonly Dictionary<string, int> labels = new(StringComparer.Ordinal);
        readonly Dictionary<string, LocalVar> localVars = new(StringComparer.Ordinal);
        readonly VarsPool[] pools = new VarsPool[4];
        readonly Dictionary<char, LocalVar> quickVarsPool = new();

        int unresolvedLabelInd;
        int cmdN;
        int cmdStartBufPos;
        int numAllocatedCompilerVars;
        int markedPos;
        bool isInStr;
        bool isErm2;
        int varPos;

        public Impl(string scriptName, string script, EraNames names, List<ErmDiagnostic>? diags)
        {
            this.scriptName = scriptName;
            this.names = names;
            this.diags = diags;
            sc = new Scanner(script);
        }

        static bool InSet(char c, string set) => set.IndexOf(c) >= 0;
        static bool IsUpper(char c) => c >= 'A' && c <= 'Z';
        static bool IsLower(char c) => c >= 'a' && c <= 'z';
        static bool IsDigit(char c) => c >= '0' && c <= '9';
        static bool IsConstChar(char c) => IsUpper(c) || IsDigit(c) || c == '_';
        static bool IsVarChar(char c) => IsUpper(c) || IsLower(c) || IsDigit(c) || c == '_';
        static bool IsIdentChar(char c) => c != '\0' && c != '(' && c != ')' && c != '\n' && c != '\r';
        static bool IsLabelChar(char c) => c != '\0' && c != ']' && c != '\n' && c != '\r';
        static bool IsSafeBlank(char c) => c >= '\x01' && c <= ' ';
        static bool IsNumberStart(char c) => c == '+' || c == '-' || IsDigit(c);

        void ShowError(int errPos, string error)
        {
            if (!sc.PosToLine(errPos, out int line, out int linePos)) { line = -1; linePos = -1; }
            string context = sc.GetSubstrAtPos(errPos - 20, 20) + " <<< " + sc.GetSubstrAtPos(errPos, 100);
            diags?.Add(new ErmDiagnostic
            {
                Severity = ErmSeverity.Error,
                Loc = new ErmSourceLoc(scriptName, line, linePos),
                Message = $"{error}. Context: {context}",
            });
        }

        void MarkPos() => markedPos = sc.Pos;

        void FlushMarked()
        {
            if (sc.Pos > markedPos)
            {
                Add(sc.GetSubstrAtPos(markedPos, sc.Pos - markedPos));
                markedPos = sc.Pos;
            }
        }

        int Add(string s, int obj = 0)
        {
            buf.Add(s);
            bufObj.Add(obj);
            return buf.Count - 1;
        }

        void Insert(int index, string s)
        {
            buf.Insert(index, s);
            bufObj.Insert(index, 0);
        }

        void SetCount(int count)
        {
            buf.RemoveRange(count, buf.Count - count);
            bufObj.RemoveRange(count, bufObj.Count - count);
        }

        int LabelValue(string name) => labels.TryGetValue(name, out int v) ? v : 0;

        void DeclareLabel(string name) => labels[name] = cmdN + 1;

        void ParseLabel(Scope scope)
        {
            FlushMarked();
            sc.GotoNextChar();
            char c = '\0';
            bool isDeclaration = sc.GetCurrChar(ref c) && c == ':';
            if (isDeclaration) sc.GotoNextChar();

            if (sc.ReadToken(IsLabelChar, out string labelName) && sc.GetCurrChar(ref c))
            {
                if (c == ']')
                {
                    sc.GotoNextChar();
                    if (isDeclaration)
                    {
                        if (scope == Scope.Global) DeclareLabel(labelName);
                        else ShowError(sc.Pos, "Label declaration inside command is prohibited");
                    }
                    else
                    {
                        if (scope == Scope.Cmd)
                        {
                            int value = LabelValue(labelName);
                            if (value == 0) unresolvedLabelInd = Add(labelName, unresolvedLabelInd);
                            else Add((value - 1).ToString(CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            FlushMarked();
                        }
                    }
                }
                else
                {
                    ShowError(sc.Pos, "Unexpected line end in label name");
                    if (!isDeclaration) Add("999999");
                }
            }
            else
            {
                ShowError(sc.Pos, "Missing closing \"]\"");
                if (!isDeclaration) Add("999999");
            }
            MarkPos();
        }

        void ResolveLabels()
        {
            int i = unresolvedLabelInd;
            while (i != NoLabel)
            {
                string name = buf[i];
                int value = LabelValue(name);
                if (value == 0)
                {
                    ShowError(sc.Pos, "Unresolved label \"" + name + "\"");
                    buf[i] = "999999";
                }
                else
                {
                    buf[i] = (value - 1).ToString(CultureInfo.InvariantCulture);
                }
                i = bufObj[i];
            }
            unresolvedLabelInd = NoLabel;
        }

        /// <summary>StrLib.SkipCharsetEx: true when a character outside the set was found (its 1-based pos).</summary>
        static bool SkipCharsetEx(Func<char, bool> set, string s, int startPos, out int charPos)
        {
            charPos = 0;
            if (startPos > s.Length) return false;
            int i = startPos;
            while (i <= s.Length && set(s[i - 1])) i++;
            if (i <= s.Length) { charPos = i; return true; }
            return false;
        }

        static bool DetectIdentType(string ident, out int identType)
        {
            identType = 0;
            if (ident.Length == 0) return false;
            char first = ident[0];
            if (IsUpper(first))
            {
                if (!SkipCharsetEx(IsConstChar, ident, 1, out int charPos)) identType = IdentTypeConst;
                else if (!SkipCharsetEx(IsVarChar, ident, charPos, out _)) identType = IdentTypeFunc;
                else return false;
                return true;
            }
            if (first == '@' || first == '-')
            {
                identType = IdentTypeVar;
                return true;
            }
            if (IsLower(first))
            {
                if (ident.IndexOf('_') < 0 || ident.IndexOf('[') >= 0) identType = IdentTypeVar;
                else if (!SkipCharsetEx(IsVarChar, ident, 1, out _)) identType = IdentTypeFunc;
                else return false;
                return true;
            }
            return false;
        }

        void InitLocalVarsPools()
        {
            pools[LocalY] = new VarsPool { StartIndex = 1, Count = 100 };
            pools[LocalX] = new VarsPool { StartIndex = 1, Count = 16 };
            pools[LocalZ] = new VarsPool { StartIndex = 1, Count = 10, IsNegative = true };
            pools[LocalE] = new VarsPool { StartIndex = 2, Count = 100 };
        }

        void FinalizeLocalVarsPools()
        {
            for (int i = 0; i < pools.Length; i++) if (pools[i] != null) pools[i].FreeRanges = null;
            localVars.Clear();
            numAllocatedCompilerVars = 0;
        }

        static int LocalVarCharToId(char c) => c switch
        {
            'y' => LocalY,
            'x' => LocalX,
            'z' => LocalZ,
            'e' => LocalE,
            _ => 0,
        };

        void FreeLocalVar(string varName)
        {
            if (!localVars.TryGetValue(varName, out var lv))
            {
                ShowError(varPos, "Cannot free local ERM variable, which was never allocated. Variable name: " + varName);
                return;
            }
            var pool = pools[LocalVarCharToId(lv.VarType)];
            if (pool.StartIndex == lv.StartIndex + lv.Count)
            {
                pool.StartIndex -= lv.Count;
                pool.Count += lv.Count;
            }
            else
            {
                pool.FreeRanges = new VarRange { StartIndex = lv.StartIndex, Count = lv.Count, NextRange = pool.FreeRanges };
            }
            localVars.Remove(varName);
        }

        bool AllocLocalVar(string varName, char varType, int count, out LocalVar? lv)
        {
            var pool = pools[LocalVarCharToId(varType)];
            bool result = false;
            lv = null;
            if (count < 0)
            {
                ShowError(varPos, "Cannot allocate local ERM variables array of " + count + " size");
                return false;
            }
            if (count == 0) count++;

            VarRange? range = pool.FreeRanges;
            VarRange? prev = null;
            while (!result && range != null)
            {
                if (range.Count >= count)
                {
                    result = true;
                    lv = new LocalVar { StartIndex = range.StartIndex, Count = count, VarType = varType, IsNegative = pool.IsNegative };
                    localVars[varName] = lv;
                    if (range.Count == count)
                    {
                        // Era unlinks with PrevRange, which its loop sets to the *current* range (a bug):
                        // a fully used range that is not the list head stays reachable. Reproduced.
                        if (prev == null) pool.FreeRanges = range.NextRange;
                        else prev.NextRange = range.NextRange;
                    }
                    else
                    {
                        range.StartIndex += count;
                        range.Count -= count;
                    }
                }
                else
                {
                    range = range.NextRange;
                }
                prev = range;
            }

            if (!result)
            {
                if (pool.Count < count)
                {
                    ShowError(varPos, "Cannot allocate more local " + varType + "-vars");
                }
                else
                {
                    result = true;
                    lv = new LocalVar { StartIndex = pool.StartIndex, Count = count, VarType = varType, IsNegative = pool.IsNegative };
                    localVars[varName] = lv;
                    pool.StartIndex += count;
                    pool.Count -= count;
                }
            }
            return result;
        }

        bool ParseLocalVar(string text, ref ParsedLocalVar pv)
        {
            int p = 0;
            char At(int k) => k < text.Length ? text[k] : '\0';

            pv.IsFreeing = At(p) == '-';
            pv.IsDeclaration = false;
            pv.HasIndex = false;
            pv.Index = 1;
            pv.IndexVar = null;

            if (pv.IsFreeing) p++;
            else
            {
                pv.IsAddr = At(p) == '@';
                if (pv.IsAddr) p++;
            }

            int start = p;
            if (!IsLower(At(p)))
            {
                ShowError(varPos, "Local variable must start with \"a\"..\"z\" character");
                return false;
            }
            while (IsLower(At(p)) || IsUpper(At(p)) || IsDigit(At(p))) p++;
            pv.Name = text.Substring(start, p - start);

            char c = At(p);
            if (c != '\0' && c != '[' && c != ':')
            {
                ShowError(varPos, "Invalid character in local variable name: \"" + c + "\"");
                return false;
            }
            if (c == '\0') return true;

            if (c == '[')
            {
                p++;
                pv.HasIndex = true;
                start = p;
                bool numeric = At(p) == '-' || IsDigit(At(p));
                if (numeric) while (At(p) == '-' || IsDigit(At(p))) p++;
                else while (IsVarChar(At(p))) p++;

                bool ok = At(p) == ']';
                if (ok)
                {
                    string token = text.Substring(start, p - start);
                    if (numeric)
                    {
                        ok = TryStrToInt(token, out int idx);
                        if (ok) pv.Index = idx;
                    }
                    else
                    {
                        ok = DetectIdentType(token, out int identType);
                        if (!ok || identType == IdentTypeFunc)
                        {
                            ShowError(varPos, $"Invalid identifier: ({token})");
                            return false;
                        }
                        if (identType == IdentTypeConst)
                        {
                            int existing;
                            if (token == MagicSizeConstName)
                            {
                                existing = MagicSizeConst;
                                if (pv.IsAddr)
                                {
                                    ShowError(varPos, "Magic constant \"" + MagicSizeConstName + "\" cannot be used with @ operator");
                                    return false;
                                }
                            }
                            else if (!names.Constants.TryGetValue(token, out existing))
                            {
                                ShowError(varPos, $"Global constant \"{token}\" is not defined");
                                return false;
                            }
                            pv.Index = existing;
                        }
                        else if (identType == IdentTypeVar)
                        {
                            if (token.Length == 1 && token[0] >= 'f' && token[0] <= 't')
                            {
                                pv.IndexVar = quickVarsPool[token[0]];
                            }
                            else if (!localVars.TryGetValue(token, out var iv))
                            {
                                ShowError(varPos, $"Usage of underclared local variable \"{token}\"");
                                return false;
                            }
                            else pv.IndexVar = iv;
                        }
                    }
                }
                if (ok) p++;
                else
                {
                    ShowError(varPos, "Invalid ERM local array subscript");
                    return false;
                }
            }

            c = At(p);
            if (c != '\0' && c != ':')
            {
                ShowError(varPos, "Unexpected local variable termination. Expected \")\" or \":\"");
                return false;
            }
            if (c == '\0') return true;

            p++;
            pv.IsDeclaration = true;
            c = At(p);
            if (c != 'x' && c != 'y' && c != 'z' && c != 'e')
            {
                ShowError(varPos, "Invalid local variable type in declaration. Expected one of \"x\", \"y\", \"z\" or \"e\"");
                return false;
            }
            pv.VarType = c;
            return true;
        }

        bool GetLocalVar(string varName, out LocalVar? lv, out int arrIndex, out LocalVar? arrVarIndex, out bool isAddr)
        {
            var pv = new ParsedLocalVar();
            lv = null;
            arrIndex = 0;
            arrVarIndex = null;
            isAddr = false;
            bool result = ParseLocalVar(varName, ref pv);
            if (!result) return false;

            isAddr = pv.IsAddr;
            arrIndex = 0;
            arrVarIndex = pv.IndexVar;
            if (pv.HasIndex && !pv.IsDeclaration && arrVarIndex == null) arrIndex = pv.Index;

            if (pv.IsFreeing)
            {
                FreeLocalVar(pv.Name);
                lv = null;
                return true;
            }

            if (!localVars.TryGetValue(pv.Name, out lv))
            {
                if (!pv.IsDeclaration)
                {
                    ShowError(varPos, $"Usage of underclared local variable \"{pv.Name}\"");
                    return false;
                }
                if (arrIndex == MagicSizeConst)
                    ShowError(varPos, "Cannot use magic \"" + MagicSizeConstName + "\" constant in array declaration");
                return AllocLocalVar(pv.Name, pv.VarType, pv.Index, out lv);
            }

            if (pv.IsDeclaration && (lv.VarType != pv.VarType || lv.Count != pv.Index))
            {
                ShowError(varPos, $"Redeclaration of local variable \"{pv.Name}\" must have the same type and length as original declaration");
                return false;
            }
            if (arrIndex == MagicSizeConst)
            {
                isAddr = true;
                arrIndex = lv.Count - lv.StartIndex;
                return true;
            }
            if (arrIndex < 0) arrIndex = lv.Count + arrIndex;
            result = arrIndex >= 0 && arrIndex < lv.Count;
            if (!result) ShowError(varPos, $"Local array index {arrIndex} is out of range: 0..{lv.Count - 1}");
            return result;
        }

        void HandleLocalVar(string varName, int varStartPos, bool isIndirectAddressing)
        {
            varPos = varStartPos;
            if (!GetLocalVar(varName, out var lv, out int arrIndex, out var indexVar, out bool isAddr))
            {
                Add("t");
                return;
            }
            if (lv == null) return; // freeing
            int varIndex = int.MinValue;
            LocalVar? tempVar = null;

            if (indexVar == null)
            {
                varIndex = lv.RealStartIndex + arrIndex;
                // The last condition part is a small hack to make an exception for SIZE constant
                if (!(arrIndex >= 0 && arrIndex <= lv.Count - 1) && (!isAddr || varIndex != lv.Count))
                {
                    ShowError(varPos, $"Array index {arrIndex} is out of [0..{lv.Count - 1}] range");
                    varIndex = Math.Clamp(varIndex, lv.RealStartIndex, lv.RealStartIndex + lv.Count - 1);
                }
            }
            else
            {
                // Allocate temporary compiler variable to hold var item pointer
                numAllocatedCompilerVars++;
                if (!AllocLocalVar(numAllocatedCompilerVars.ToString(CultureInfo.InvariantCulture), 'y', 1, out tempVar))
                {
                    numAllocatedCompilerVars--;
                    ShowError(varPos, "Cannot allocate y-variable for array pointer");
                    Add("t");
                    return;
                }
                string idx = indexVar.VarType >= 'f' && indexVar.VarType <= 't'
                    ? indexVar.VarType.ToString()
                    : indexVar.VarType + indexVar.RealStartIndex.ToString(CultureInfo.InvariantCulture);
                Insert(cmdStartBufPos, string.Format(CultureInfo.InvariantCulture, "!!VRy{0}:S{1} +{2} F{1}/{3}/0/0; ",
                    tempVar!.RealStartIndex, lv.RealStartIndex, idx, lv.RealStartIndex + lv.Count - 1));
                cmdStartBufPos++;
            }

            if (!isAddr)
            {
                if (isInStr && !isIndirectAddressing) Add("%");
                if (indexVar == null) Add(lv.VarType + varIndex.ToString(CultureInfo.InvariantCulture));
                else Add(lv.VarType + "y" + tempVar!.StartIndex.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                if (indexVar == null) Add(varIndex.ToString(CultureInfo.InvariantCulture));
                else if (!isInStr) Add("y" + tempVar!.StartIndex.ToString(CultureInfo.InvariantCulture));
                else Add("%y" + tempVar!.StartIndex.ToString(CultureInfo.InvariantCulture));
            }
        }

        static bool TryStrToInt(string s, out int v) =>
            int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v);

        void HandleConstDeclaration()
        {
            sc.GotoRelPos(-2);
            FlushMarked();
            sc.GotoRelPos(+4);
            int startPos = 0;
            int constValue = 0;
            string constName = "";
            bool result = sc.C == '(';
            if (!result) ShowError(sc.Pos, "Expected \"(\" character in const declaration");
            else
            {
                sc.GotoNextChar();
                startPos = sc.Pos;
                sc.SkipCharset(IsConstChar);
                result = sc.C == ')';
                if (!result) ShowError(sc.Pos, "Invalid constant name. Expected [A-Z0-9_] characters");
            }

            if (result)
            {
                constName = sc.GetSubstrAtPos(startPos, sc.Pos - startPos);
                sc.GotoNextChar();
                sc.SkipCharset(IsSafeBlank);
                result = sc.C == '=';
                if (!result) ShowError(sc.Pos, "Expected \"=\" character and constant value");
            }

            bool isAlias = false;
            if (result)
            {
                sc.GotoNextChar();
                sc.SkipCharset(IsSafeBlank);
                isAlias = sc.C == '(';
                result = isAlias || IsNumberStart(sc.C);
                if (!result) ShowError(sc.Pos, "Expected valid integer or existing constant name as constant value");
            }

            if (result)
            {
                if (isAlias)
                {
                    sc.GotoNextChar();
                    startPos = sc.Pos;
                    sc.SkipCharset(ch => IsConstChar(ch) || ch == ')');
                }
                else
                {
                    startPos = sc.Pos;
                    sc.GotoNextChar();
                    sc.SkipCharset(IsDigit);
                }
                result = sc.C == ';';
                if (!result) ShowError(sc.Pos, "Expected \";\" character as const declaration end marker");
            }

            if (result)
            {
                string token = sc.GetSubstrAtPos(startPos, sc.Pos - startPos - (isAlias ? 1 : 0));
                if (isAlias)
                {
                    result = names.Constants.TryGetValue(token, out constValue);
                    if (!result) ShowError(startPos, $"Global constant \"{token}\" is not defined");
                }
                else
                {
                    result = TryStrToInt(token, out constValue);
                    if (!result) ShowError(startPos, "Expected valid integer as constant value");
                }
            }

            if (result)
            {
                bool exists = names.Constants.TryGetValue(constName, out int existing);
                result = !exists || existing == constValue;
                if (!result) ShowError(startPos, $"Global constant \"{constName}\" is already defined with value {existing}");
                else names.Constants[constName] = constValue;
            }

            // Recover from error
            if (!result) sc.FindCharset(ch => ch == ';');
            MarkPos();
        }

        void ParseIdent(bool isIndirectAddressing = false)
        {
            int startPos = sc.Pos;
            sc.GotoNextChar();
            char c = '\0';
            if (sc.ReadToken(IsIdentChar, out string ident) && sc.GetCurrChar(ref c))
            {
                if (c == ')')
                {
                    sc.GotoNextChar();
                    if (ident.Length == 0)
                    {
                        ShowError(startPos, "Empty string is not a valid identifier: (nothing inside parantheses)");
                        Add("999999");
                    }
                    else if (!isErm2)
                    {
                        names.AllocFunction(ident, out int funcId);
                        Add(funcId.ToString(CultureInfo.InvariantCulture));
                    }
                    else if (!DetectIdentType(ident, out int identType))
                    {
                        ShowError(startPos, "Invalid identifier: (" + ident + ")");
                        Add("999999");
                    }
                    else
                    {
                        switch (identType)
                        {
                            case IdentTypeVar:
                                HandleLocalVar(ident, startPos, isIndirectAddressing);
                                break;
                            case IdentTypeConst:
                                HandleConst(ident, startPos);
                                break;
                            case IdentTypeFunc:
                                names.AllocFunction(ident, out int funcId);
                                Add(funcId.ToString(CultureInfo.InvariantCulture));
                                break;
                        }
                    }
                }
                else
                {
                    ShowError(sc.Pos, "Unexpected character in identifier name");
                    Add("999999");
                }
            }
            else
            {
                ShowError(sc.Pos, "Missing closing \")\"");
                Add("999999");
            }
        }

        void HandleConst(string ident, int startPos)
        {
            if (ident == "FILE")
            {
                Add(isInStr ? scriptName : "^" + scriptName + "^");
            }
            else if (ident == "LINE")
            {
                Add(sc.LineN.ToString(CultureInfo.InvariantCulture));
            }
            else if (ident == "CODE")
            {
                int savedPos = sc.Pos;
                int lineStartPos = sc.LineStartPos + 1;
                if (sc.GotoNextLine()) sc.GotoPrevChar();
                if (sc.Pos > lineStartPos)
                {
                    string excerpt = sc.GetSubstrAtPos(lineStartPos, sc.Pos - lineStartPos);
                    if (excerpt.Length > 100) excerpt = excerpt.Substring(0, 100);
                    excerpt = EscapeErmLiteralContents(excerpt);
                    Add(isInStr ? excerpt : "^" + excerpt + "^");
                }
                sc.GotoPos(savedPos);
            }
            else if (names.Constants.TryGetValue(ident, out int value))
            {
                Add(value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                ShowError(startPos, "Unknown global constant name: \"" + ident + "\". Assuming 0");
                names.Constants[ident] = 0;
                Add("t");
            }
        }

        void ParseCmd(CmdType cmdType)
        {
            sc.GotoNextChar();
            sc.GotoRelPos(-2);
            FlushMarked();
            sc.GotoRelPos(+2);
            cmdStartBufPos = buf.Count;

            if (cmdType == CmdType.Instruction && sc.C == 'D' && sc.CharsRel(1) == 'C')
            {
                HandleConstDeclaration();
                return;
            }

            int dummyCmdBufPos = -1;
            if (cmdType == CmdType.Instruction && sc.C == 'V' && sc.CharsRel(1) == 'A')
                dummyCmdBufPos = cmdStartBufPos;

            char c = ' ';
            while (sc.FindCharset(ch => ch == '[' || ch == '(' || ch == '^' || ch == ';' || ch == '%')
                   && sc.GetCurrChar(ref c) && (c != ';' || isInStr))
            {
                switch (c)
                {
                    case '[':
                        // Era reuses "c" here, which matters when the file ends inside the command
                        if (!isInStr && sc.GetCharAtRelPos(+1, ref c) && c != ':') ParseLabel(Scope.Cmd);
                        else sc.GotoNextChar();
                        break;
                    case '(':
                        if (!isInStr)
                        {
                            FlushMarked();
                            ParseIdent();
                            MarkPos();
                        }
                        else sc.GotoNextChar();
                        break;
                    case '^':
                        sc.GotoNextChar();
                        isInStr = !isInStr;
                        break;
                    case '%':
                        if (isInStr)
                        {
                            char next = sc.CharsRel(1);
                            // Special support for indirect addressing using local variables in interpolated strings
                            // Example: %y(artPtr) may be equal to %yx%(@artPtr) and compile to %yx3
                            if ((IsLower(next) || IsUpper(next)) && next != 's' && next != 'S' && next != 'i' && next != 'I' && next != 'T'
                                && sc.CharsRel(2) == '(')
                            {
                                sc.GotoRelPos(+2);
                                FlushMarked();
                                ParseIdent(isIndirectAddressing: true);
                                MarkPos();
                            }
                            else
                            {
                                switch (next)
                                {
                                    case '(':
                                        FlushMarked();
                                        sc.GotoNextChar();
                                        ParseIdent();
                                        MarkPos();
                                        break;
                                    case '%':
                                        sc.GotoRelPos(+2);
                                        break;
                                    default:
                                        sc.GotoNextChar();
                                        break;
                                }
                            }
                        }
                        else sc.GotoNextChar();
                        break;
                    case ';':
                        sc.GotoNextChar();
                        break;
                }
            }

            if (c == ';')
            {
                sc.GotoNextChar();
                cmdN++;
                // Release compiler temp variables
                int n = numAllocatedCompilerVars;
                for (int i = 1; i <= n; i++)
                {
                    FreeLocalVar(i.ToString(CultureInfo.InvariantCulture));
                    numAllocatedCompilerVars = 0;
                }
                // Erase anything, written during dummy command parsing
                if (dummyCmdBufPos != -1)
                {
                    SetCount(dummyCmdBufPos);
                    MarkPos();
                }
            }
            isInStr = false;
        }

        public string Run()
        {
            markedPos = 1;
            cmdN = 999000; // CmdN must not be used in instructions
            cmdStartBufPos = 0;
            numAllocatedCompilerVars = 0;
            unresolvedLabelInd = NoLabel;
            isErm2 = IsErm2(sc.Text);
            isInStr = false;
            InitLocalVarsPools();
            for (char q = 'f'; q <= 't'; q++)
                quickVarsPool[q] = new LocalVar { StartIndex = 0, Count = 1, VarType = q, IsNegative = false };

            char c = '\0';
            while (sc.FindCharset(ch => ch == '[' || ch == '!'))
            {
                sc.GetCurrChar(ref c);
                switch (c)
                {
                    case '!':
                        sc.GotoNextChar();
                        if (sc.GetCurrChar(ref c))
                        {
                            switch (c)
                            {
                                case '!':
                                    if (sc.GetCharAtRelPos(+1, ref c) && c == '!')
                                    {
                                        FlushMarked();
                                        sc.SkipChars('!');
                                        MarkPos();
                                    }
                                    else ParseCmd(CmdType.Receiver);
                                    break;
                                case '?':
                                case '$':
                                    if (isErm2)
                                    {
                                        FinalizeLocalVarsPools();
                                        InitLocalVarsPools();
                                    }
                                    ResolveLabels();
                                    labels.Clear();
                                    cmdN = -1;
                                    ParseCmd(CmdType.Trigger);
                                    break;
                                case '#':
                                    ParseCmd(CmdType.Instruction);
                                    break;
                            }
                        }
                        break;
                    case '[':
                        if (sc.GetCharAtRelPos(+1, ref c) && c == ':') ParseLabel(Scope.Global);
                        else sc.GotoNextChar();
                        break;
                }
            }

            if (isErm2) FinalizeLocalVarsPools();

            if (markedPos == 1) return sc.Text;
            FlushMarked();
            ResolveLabels();
            var sb = new StringBuilder();
            foreach (var s in buf) sb.Append(s);
            return sb.ToString();
        }
    }

    /// <summary>EscapeErmLiteralContents: ';' → %\:, '^' → %\", '%' → %%.</summary>
    public static string EscapeErmLiteralContents(string literal)
    {
        var sb = new StringBuilder(literal.Length + 8);
        foreach (char c in literal)
        {
            switch (c)
            {
                case ';': sb.Append("%\\:"); break;
                case '^': sb.Append("%\\\""); break;
                case '%': sb.Append("%%"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Port of B2 TextScan.TTextScanner (1-based positions, #10 line marker).</summary>
    sealed class Scanner
    {
        public string Text { get; }
        public int Pos { get; private set; } = 1;
        public int LineN { get; private set; } = 1;
        public int LineStartPos { get; private set; }
        public bool Eot { get; private set; }
        readonly int len;

        public Scanner(string text)
        {
            Text = text;
            len = text.Length;
            Eot = len == 0;
        }

        bool IsValidPos(int p) => p >= 1 && p <= len + 1;
        public char C => Eot ? '\0' : Text[Pos - 1];

        // Delphi "out" char parameters keep their value when the call fails; "ref" reproduces that.
        public bool GetCurrChar(ref char c)
        {
            if (Eot) return false;
            c = Text[Pos - 1];
            return true;
        }

        public bool GetCharAtPos(int p, ref char c)
        {
            if (!(IsValidPos(p) && p <= len)) return false;
            c = Text[p - 1];
            return true;
        }

        public bool GetCharAtRelPos(int rel, ref char c) => GetCharAtPos(Pos + rel, ref c);

        public char CharsRel(int rel)
        {
            char c = '\0';
            return GetCharAtPos(Pos + rel, ref c) ? c : '\0';
        }

        public string GetSubstrAtPos(int targetPos, int substrLen)
        {
            int startPos = targetPos;
            if (startPos < 1)
            {
                startPos = 1;
                substrLen = Math.Max(0, substrLen + targetPos - 1);
            }
            else if (startPos > len + 1)
            {
                startPos = len + 1;
            }
            int endPos = Math.Clamp(targetPos + substrLen, 1, len + 1);
            int count = endPos - startPos;
            return count <= 0 ? "" : Text.Substring(startPos - 1, count);
        }

        public bool GotoNextChar()
        {
            if (Eot) return false;
            if (Text[Pos - 1] == '\n')
            {
                LineStartPos = Pos;
                LineN++;
            }
            Pos++;
            if (Pos > len)
            {
                Eot = true;
                return false;
            }
            return true;
        }

        public bool GotoPrevChar()
        {
            if (Pos <= 1) return false;
            Pos--;
            if (Text[Pos - 1] == '\n')
            {
                LineN--;
                int i = Pos - 1;
                while (i >= 1 && Text[i - 1] != '\n') i--;
                LineStartPos = i;
            }
            Eot = false;
            return true;
        }

        public bool GotoPos(int target)
        {
            if (!IsValidPos(target)) return false;
            int steps = Math.Abs(target - Pos);
            if (target >= Pos) for (int i = 0; i < steps; i++) GotoNextChar();
            else for (int i = 0; i < steps; i++) GotoPrevChar();
            return true;
        }

        public bool GotoRelPos(int rel) => GotoPos(Pos + rel);

        public bool GotoNextLine()
        {
            int orig = LineN;
            while (LineN == orig && GotoNextChar()) { }
            return LineN > orig;
        }

        public bool PosToLine(int target, out int lineN, out int linePos)
        {
            lineN = 0;
            linePos = 0;
            int curr = Pos;
            if (!GotoPos(target)) return false;
            lineN = LineN;
            linePos = Pos - LineStartPos;
            GotoPos(curr);
            return true;
        }

        public bool SkipChars(char ch)
        {
            if (Eot) return false;
            while (Text[Pos - 1] == ch && GotoNextChar()) { }
            return !Eot;
        }

        public bool SkipCharset(Func<char, bool> set)
        {
            if (Eot) return false;
            while (set(Text[Pos - 1]) && GotoNextChar()) { }
            return !Eot;
        }

        public bool FindCharset(Func<char, bool> set)
        {
            if (Eot) return false;
            while (!set(Text[Pos - 1]) && GotoNextChar()) { }
            return !Eot;
        }

        public bool ReadToken(Func<char, bool> set, out string token)
        {
            token = "";
            if (Eot) return false;
            int start = Pos;
            SkipCharset(set);
            token = Text.Substring(start - 1, Pos - start);
            return true;
        }
    }
}
