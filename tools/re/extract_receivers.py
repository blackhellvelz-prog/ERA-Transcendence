# Extracts, for every ERM receiver registered in WoG's ERM_Addition[] table, the command letters its
# implementing function dispatches on.
# Usage: python3 extract_receivers.py <dir with WoG T1/*.cpp converted from cp1251 to UTF-8> out.json
# (source: github.com/GrayFace/wog, see WoG_ReverseEngineering/00_Sources.md)
import re,glob,json,sys
import os
srcdir=sys.argv[1]
src={f:open(f).read() for f in glob.glob(os.path.join(srcdir,'*.cpp'))}
erm=src[os.path.join(srcdir,'erm.cpp')]
tab=re.search(r'ERM_Addition\[\]=\{(.*?)\{0,0\}',erm,re.S).group(1)
out={}
def body(fn):
    for f,t in src.items():
        m=re.search(r'\nint\s+(?:__fastcall\s+)?'+fn+r'\s*\(char\s+Cmd[^)]*\)\s*\{',t)
        if m:
            i=m.end(); depth=1
            while depth and i<len(t):
                if t[i]=='{': depth+=1
                elif t[i]=='}': depth-=1
                i+=1
            return f.split('/')[-1],t[m.start():i]
    return None,None
for rid,fn,tp in re.findall(r"\{'(\w\w)',(\w+),(\d+)\}",tab):
    f,b=body(fn)
    letters=[]
    if b:
        # top-level switch(Cmd) cases (single char)
        for c in re.findall(r"case\s+'(.)'\s*:",b):
            if c not in letters: letters.append(c)
        for c in re.findall(r"Cmd\s*==\s*'(.)'",b):
            if c not in letters: letters.append(c)
    out[rid]={'fn':fn,'file':f,'type':int(tp),'cmds':letters}
# inline receivers in ProcessMes
pm=erm[erm.index('int ProcessMes('):erm.index('void ProcessCmd(')]
marks=[(m.start(),m.group(1)) for m in re.finditer(r"case 0x[0-9A-F]+: // '?(\w\w)",pm)]
marks.append((len(pm),None))
for (s,rid),(e,_) in zip(marks,marks[1:]):
    b=pm[s:e]; letters=[]
    for c in re.findall(r"Cmd\s*==\s*'(.)'",b)+re.findall(r"case\s+'(.)'\s*:",b):
        if c not in letters: letters.append(c)
    out[rid]={'fn':'ProcessMes','file':'erm.cpp','type':'inline','cmds':letters}
json.dump(out,open(sys.argv[2],'w'),indent=1)
for k,v in out.items(): print(k,v['fn'],v['file'],''.join(v['cmds']))
