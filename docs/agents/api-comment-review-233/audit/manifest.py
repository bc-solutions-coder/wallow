import pathlib,json,csv,hashlib,subprocess,sys,xml.etree.ElementTree as ET,xml.parsers.expat as xp
import bashlex
from markdown_it import MarkdownIt
ROOT=pathlib.Path(sys.argv[1]).resolve()
BASE='258f27eabbe6676598a79097ff99cae899dc0e81'
OUT=ROOT/'docs/agents/api-comment-review-233'
def bashcomments(s):
 # The lexer recognizes comment gaps without mistaking quoted # characters for comments.
 covered=set()
 for t in bashlex.parser._parser(s).tok:covered.update(range(t.lexpos,t.endlexpos))
 out=[];offset=0
 for i,line in enumerate(s.splitlines(keepends=True)):
  for k,ch in enumerate(line):
   if ch=='#' and offset+k not in covered:
    out.append((i,k,line[k:].rstrip('\r\n')));break
  offset+=len(line)
 return out
def mdcomments(s):
 out=[]
 for t in MarkdownIt().parse(s):
  if t.type not in ['fence','code_block','html_block']:continue
  if t.info=='bash':found=bashcomments(t.content)
  elif t.type=='html_block':found=[(0,0,t.content)] if '<!--' in t.content else []
  elif t.info=='':found=[(i,l.index('#'),l[l.index('#'):]) for i,l in enumerate(t.content.splitlines()) if '#' in l]
  else:found=[] # inspected C# and JSON fences contain no comments
  for i,k,text in found:out.append(dict(line=t.map[0]+(2 if t.type=='fence' else 1)+i,text=text))
 return out
def xmlvalue(b):
 def node(n):return(n.tag,sorted(n.attrib.items()),n.text if n.text and n.text.strip() else '',n.tail if n.tail and n.tail.strip() else '',[node(c) for c in n])
 return node(ET.fromstring(b))
def stripped(s,cs):
 lines=s.splitlines()
 for c in sorted(cs,key=lambda c:c['line'],reverse=True):
  i=c['line']-1;text=c['text'];assert text in lines[i],(i,text,lines[i]);lines[i]=lines[i].replace(text,'',1)
 return '\n'.join(l.rstrip() for l in lines if l.strip())
def ast(n):
 if isinstance(n,bashlex.ast.node):return {k:ast(v) for k,v in vars(n).items() if k!='pos'}
 if isinstance(n,list):return [ast(v) for v in n]
 return n

files=list(csv.DictReader((OUT/'files.tsv').open(),delimiter='\t'))
comments=list(csv.DictReader((OUT/'comments.tsv').open(),delimiter='\t'))
paths=set(subprocess.check_output(['git','ls-files','api'],cwd=ROOT,text=True).splitlines())
assert paths=={r['path'] for r in files} and len(paths)==len(files)
assert len({(r['path'],r['baseline_line']) for r in comments})==len(comments)
count=0
for r in files:
 p=r['path'];b=subprocess.check_output(['git','show',BASE+':'+p],cwd=ROOT);f=(ROOT/p).read_bytes()
 assert hashlib.sha256(b).hexdigest()==r['baseline_sha256'],p
 assert hashlib.sha256(f).hexdigest()==r['final_sha256'],p
 assert r['evidence'] and not r['unresolved'] and r['disposition']!='pending',p
 if r['disposition']=='generated-preserved':assert b==f,p
 if p.endswith('.cs'):continue
 old=b.decode('utf-8-sig');new=f.decode('utf-8-sig');ext=pathlib.Path(p).suffix
 def inventory(s):
  if ext=='.md':return mdcomments(s)
  if ext=='.json':json.loads(s);return []
  if ext=='.sh':return [dict(line=i+1,text=t) for i,k,t in bashcomments(s)]
  if p.endswith('.editorconfig'):return [dict(line=i,text=l) for i,l in enumerate(s.splitlines(),1) if l.startswith(('#',';'))]
  parser=xp.ParserCreate();cs=[]
  parser.CommentHandler=lambda text:cs.append(dict(line=parser.CurrentLineNumber,text=text))
  parser.Parse(s,True);return cs
 cs=inventory(old)
 assert {int(c['baseline_line']) for c in comments if c['path']==p}=={c['line'] for c in cs},p
 if ext=='.md' or p.endswith('.editorconfig'):assert stripped(old,cs)==stripped(new,inventory(new)),p
 elif ext=='.json':assert json.loads(old)==json.loads(new),p
 elif ext=='.sh':
  assert ast(bashlex.parse(old))==ast(bashlex.parse(new)),p
  assert old.splitlines()[0]==new.splitlines()[0],p
  def tokens(s):return [(t.ttype.name,s[t.lexpos:t.endlexpos]) for t in bashlex.parser._parser(s).tok if t.ttype.name!='NEWLINE']
  assert tokens(old)==tokens(new),p
 else:assert xmlvalue(b)==xmlvalue(f),p
 count+=1
assert all(r['disposition'] in ['keep','rewrite','remove','protected','move'] and r['evidence'] for r in comments)
assert all(r['destination'] for r in comments if r['disposition']=='move')
print('PASS:',len(files),'file hashes and dispositions;',len(comments),'comment records;',count,'parsed non-C# comparisons.')

for r in csv.DictReader((OUT/'generated.tsv').open(),delimiter='\t'):
 before=subprocess.check_output(['git','show',BASE+':'+r['path']],cwd=ROOT)
 after=(ROOT/r['path']).read_bytes()
 assert hashlib.sha256(before).hexdigest()==r['baseline_sha256'],r['path']
 assert hashlib.sha256(after).hexdigest()==r['final_sha256'],r['path']
print('PASS: generated output hashes match the review record.')
