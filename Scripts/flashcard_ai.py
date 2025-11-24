import os, sys, re, json, time
try:
    from llama_cpp import Llama
except ImportError:
    print("? CRITICAL: llama-cpp-python is missing. Run: pip install llama-cpp-python", flush=True)
    sys.exit(1)

# Optional parsers (not used; C# handles extraction) kept for potential future extension
try: import docx
except ImportError: docx = None
try: import pptx
except ImportError: pptx = None
try: import fitz
except ImportError: fitz = None

PROMPT = (
    "{IDENTIFICATION}Create identification Q&A pairs from this lesson chunk. "
    "RULES:1. Create pairs for: **definitions, names, dates, and other specific terms.**"
    "2. DEFINITION/TERM PRIORITY: COPY AND PASTE THE EXACT DEFINITION. "
    "3. FORMAT IS STRICT: You MUST enclose every pair in bars like this: "
    "|Question: [definition text]? Answer: [term]| "
    "Output: \"\"---Lesson Chunk:\n"
)

class SmartFlashcardCleaner:
    def __init__(self): self.seen = set()
    def smart_cloze(self, q, a):
        if not q or not a: return q
        a = a.strip(); esc = re.escape(a)
        if a.lower().endswith('y'):
            pat = rf"\b{esc[:-1]}(y|ies)\b"
        elif a.lower().endswith('s'):
            pat = rf"\b{esc}(es)?\b"
        else:
            pat = rf"\b{esc}(s|es|ly|'s)?\b"
        return re.sub(pat, "_______", q, flags=re.I)
    def process(self, raw):
        cards = []
        matches = re.findall(r"\|\s*Question:\s*(.*?)(?:\?|\s)\s*Answer:\s*(.*?)\s*\|", raw, re.I | re.S)
        for q,a in matches:
            q=q.strip(); a=a.strip()
            if not q or not a: continue
            sig = f"{q.lower()}|{a.lower()}"
            if sig in self.seen: continue
            self.seen.add(sig)
            masked = self.smart_cloze(q, a) + '?' if not q.endswith('?') else self.smart_cloze(q, a)
            cards.append({"Question": masked, "Answer": a})
        return cards

class MindVaultPCGenerator:
    def __init__(self, model_path):
        if not os.path.exists(model_path):
            raise FileNotFoundError(f"Model not found: {model_path}")
        print(f"?? Loading AI Brain: {os.path.basename(model_path)}", flush=True)
        self.llm = Llama(
            model_path=model_path,
            n_ctx=2048,
            n_gpu_layers=-1,
            verbose=False,
            n_threads=os.cpu_count()
        )
        self.cleaner = SmartFlashcardCleaner()
        print("? Model loaded", flush=True)

    def _token_count(self, text):
        return len(self.llm.tokenize(text.encode('utf-8')))

    def _chunk(self, text, max_tokens=280):  # updated to 512-token chunks
        sentences = re.split(r'(?<=[.!?])\s+', text)
        chunks=[]; cur=[]; count=0
        for s in sentences:
            if not s.strip(): continue
            t = self._token_count(s)
            if t > max_tokens:
                words = s.split()
                seg=[]; seg_tokens=0
                for w in words:
                    wt = self._token_count(w + ' ')
                    if seg_tokens + wt > max_tokens and seg:
                        chunks.append(' '.join(seg))
                        seg=[]; seg_tokens=0
                    seg.append(w); seg_tokens += wt
                if seg: chunks.append(' '.join(seg))
                continue
            if count + t > max_tokens and cur:
                chunks.append(" ".join(cur)); cur=[]; count=0
            cur.append(s); count += t
        if cur: chunks.append(" ".join(cur))
        return chunks

    def generate(self, lesson_path):
        if not os.path.exists(lesson_path):
            raise FileNotFoundError(lesson_path)
        text = open(lesson_path, 'r', encoding='utf-8', errors='ignore').read()
        chunks = self._chunk(text)
        print(f"::TOTAL::{len(chunks)}", flush=True)
        all_cards=[]
        start=time.time()
        for i,ch in enumerate(chunks,1):
            print(f"::CHUNK::{i}::{len(chunks)}", flush=True)
            # Adopt tester script generation settings
            resp = self.llm(
                f"User: {PROMPT}{ch}\nAssistant:",
                max_tokens=300,
                temperature=0.0,
                top_k=1,
                repeat_penalty=1.2,
                echo=False
            )
            raw = resp['choices'][0]['text']
            cards = self.cleaner.process(raw)
            all_cards.extend(cards)
        dur = time.time() - start
        print(f"::DONE::{len(all_cards)}::{dur:.1f}", flush=True)
        with open('flashcards.json','w',encoding='utf-8') as f:
            json.dump(all_cards, f, indent=2)
        return all_cards

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print('Usage: python flashcard_ai.py <lesson_file>', flush=True)
        sys.exit(1)
    model_path = os.path.join('Models','mindvault_qwen2_0.5b_q4_k_m.gguf')
    gen = MindVaultPCGenerator(model_path)
    gen.generate(sys.argv[1])
