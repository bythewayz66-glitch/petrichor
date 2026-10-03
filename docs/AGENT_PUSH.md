# AGENT_PUSH — how an agent can write to this repository

This document exists because "can the agent push?" has four different answers
depending on which credential you mean, and three of them are not the same
thing. It records what was **actually tested**, on what date, and what was
**not** tested and why.

Tested 2026-10-03 against `bythewayz66-glitch/petrichor` at `main` =
`e7b14d179fd6b9801aaa83ffe71a37c36f6c149f`.

---

## 1. The short version

| Path | Works? | Credential | Who creates it |
|---|---|---|---|
| **GitHub API tools** (MCP) | **Yes — tested** | none in the sandbox; the tool holds it | already configured |
| **`GITHUB_TOKEN` in Actions** | **Yes — tested**, but only with `permissions: contents: write` | automatic | nobody; it is issued per run |
| **PAT over HTTPS** | **Untested** — no PAT exists | fine-grained or classic PAT | **the repository owner** |
| **Deploy key over SSH** | **Untested** — no key exists | an SSH keypair | **the repository owner** |
| **Anonymous HTTPS clone** | **Yes — tested** (read only) | none | n/a |

The repository is **public**, so anyone can *read* it with no credential at
all. Writing is the part that needs a credential.

---

## 2. Path A — the GitHub API tools (works today, no setup)

This is the path every agent run so far has used. It needs no token in the
sandbox: the tool holds the credential and the sandbox never sees it.

**Verified working** (each of these was executed, not assumed):

| Operation | Result |
|---|---|
| Read repository metadata | 200 |
| Read the file tree (recursive) | 200 |
| Read a file | 200 |
| Create or update a file | 201 |
| Create a commit | 201 |
| Delete a file | 200 |
| Delete a branch reference | 204 |
| List / create / edit / close issues | 200 / 201 / 200 / 200 |
| List / create labels | 200 / 201 |
| List / create milestones | 200 / 201 |
| List repository secrets | 200 |
| **Create a repository secret** | **201** |
| **Delete a repository secret** | **204** |
| Change repository visibility | 200 |
| Dispatch a workflow | 204 |
| List workflow runs / jobs / download job logs | 200 |
| Read branch protection / rulesets | 404 (none set) / 200 |
| Read default workflow permissions | 200 |

**What this path cannot do:** it cannot run `git`. It writes one file per
commit through the API. There is no `git push`, no branch, no merge, no
history rewrite. For a multi-file change it is one API call per file, and
each call is its own commit unless you build a tree and commit by hand.

**The important consequence:** an agent *can* install the `UNITY_LICENSE`
secret — the write permission is there (201). What it does not have is the
**value**, which only the owner can produce. See section 6.

---

## 3. Path B — `GITHUB_TOKEN` inside Actions (works, with one condition)

**Tested empirically** with a throwaway workflow that pushed to two throwaway
branches. Both jobs reported a green tick. **Only one of them actually
pushed** — which is the whole reason this section exists.

| Job | `permissions:` | Result | Evidence |
|---|---|---|---|
| `probe-default` | *(none — inherits the repo default)* | **403 denied** | `refs/heads/probe/token-default` does not exist |
| `probe-write` | `contents: write` | **pushed** | `refs/heads/probe/token-write` = `4baff182b7cc6355bb1edf68283de5e31c957e4d` |

The failure, verbatim from the job log:

```
GITHUB_TOKEN Permissions
  Contents: read
  Metadata: read
  Packages: read
...
remote: Permission to bythewayz66-glitch/petrichor.git denied to github-actions[bot].
fatal: unable to access 'https://github.com/bythewayz66-glitch/petrichor/':
       The requested URL returned error: 403
PUSH_EXIT=128
```

The repository default is `read` (`default_workflow_permissions: "read"`), so
a job that does not ask for more gets a read-only token. To push from a
workflow:

```yaml
jobs:
  commit-something:
    runs-on: ubuntu-latest
    permissions:
      contents: write        # <-- without this the push is a 403
    steps:
      - uses: actions/checkout@v4
      - run: |
          git config user.email "agent@example.invalid"
          git config user.name  "agent"
          # ... make changes ...
          git commit -am "chore: whatever"
          git push
```

**Two traps in this path:**

1. **A failed push does not fail the job.** `git push` inside a `run:` block
   that ends with `| tail` or `|| true` swallows the exit code, and the job
   goes green. The probe's default-permission job was green while its push
   was denied. If a workflow's job is to push, it must check the exit code
   explicitly — `set -e` alone is not enough once you pipe.
2. **`GITHUB_TOKEN` pushes do not trigger further workflows.** GitHub
   suppresses workflow runs caused by the token, to prevent recursion. A
   commit pushed by a workflow will not start the Water Gate.

---

## 4. Path C — a PAT over HTTPS (untested; needs the owner)

**Not tested, because no PAT exists in this environment.** What *was* tested
is the failure mode without one:

```
$ GIT_TERMINAL_PROMPT=0 git push origin main
fatal: could not read Username for 'https://github.com': terminal prompts disabled
push_exit=128
```

To enable it, the owner creates a token and stores it as a **secret** — never
in a file, never in a commit, never pasted into a chat.

**Fine-grained PAT (preferred — least privilege):**

| Setting | Value |
|---|---|
| Repository access | **Only select repositories** → `bythewayz66-glitch/petrichor` |
| Permissions → Repository → **Contents** | **Read and write** |
| Permissions → Repository → Metadata | Read (mandatory, auto-selected) |
| Everything else | No access |

**Classic PAT (broader, use only if fine-grained is unavailable):** scope
`repo`. This grants write to **every** repository the account can reach, not
just this one.

Then, in the sandbox:

```bash
git clone https://x-access-token:<TOKEN>@github.com/bythewayz66-glitch/petrichor.git
# or, to keep the token out of the remote URL:
git remote set-url origin https://github.com/bythewayz66-glitch/petrichor.git
git config credential.helper store   # writes ~/.git-credentials — treat as a secret
```

**Security tradeoff, stated plainly:** a repo-scoped PAT with `Contents:
write` is a **full write credential to this repository**. It can rewrite
`main`, delete branches, and alter workflow files. It must live in a secret
store, must never be committed, and should be rotated if it is ever exposed.
Because the repository is now **public**, a leaked token is exploitable by
anyone who finds it — the blast radius of a leak went up when visibility
changed.

---

## 5. Path D — a deploy key over SSH (untested; needs the owner)

**Not tested, because no key exists.** `GITHUB_LIST_DEPLOY_KEYS` returns
`{"deploy_keys": []}`. The failure mode without one:

```
$ ssh -T git@github.com
git@github.com: Permission denied (publickey).        # exit 255

$ git ls-remote git@github.com:bythewayz66-glitch/petrichor.git
git@github.com: Permission denied (publickey).
fatal: Could not read from remote repository.         # exit 128
```

To enable it, the owner generates a keypair and adds the **public** half as a
deploy key with **Allow write access** checked:

```bash
ssh-keygen -t ed25519 -C "petrichor-agent" -f ~/.ssh/petrichor_agent -N ""
cat ~/.ssh/petrichor_agent.pub      # paste this into the deploy key form
```

Then in the sandbox:

```bash
export GIT_SSH_COMMAND="ssh -i ~/.ssh/petrichor_agent -o IdentitiesOnly=yes"
git clone git@github.com:bythewayz66-glitch/petrichor.git
```

**Deploy key vs PAT:** a deploy key is scoped to **one repository** and cannot
touch anything else, which makes it the safer of the two for a single-repo
agent. Its downside is that it is a key, not a token — it does not expire
unless you set an expiry, and it cannot be scoped to a subset of operations
(write access is write access).

---

## 6. The `UNITY_LICENSE` secret

The gate job has never executed because this secret does not exist
(`total_count: 0`). **An agent can install it** — the secret-write path
returns 201 — but an agent cannot *produce* it. A Unity licence is a
credential only the account holder can generate.

**Do not paste a `.ulf` file into a chat.** It is a licence credential;
anything pasted into a conversation is stored in history and would have to be
revoked and reissued. Add it directly in the repository settings.

1. Activate a personal licence on a machine with the editor installed, so
   Unity writes `Unity_lic.ulf`:

   | Platform | Path |
   |---|---|
   | Linux | `~/.local/share/unity3d/Unity/Unity_lic.ulf` |
   | Windows | `C:\ProgramData\Unity\Unity_lic.ulf` |
   | macOS | `/Library/Application Support/Unity/Unity_lic.ulf` |

2. Copy the **whole** file, including the `<?xml ... ?>` declaration and the
   closing `</root>` tag. A partial copy is the most common activation
   failure.
3. Repository → **Settings** → **Secrets and variables** → **Actions** →
   **New repository secret**.
4. Name it exactly **`UNITY_LICENSE`**. A typo leaves the gate skipped and
   the run green.
5. Paste the file contents as the value.

**Alternative (Plus/Pro):** three secrets — `UNITY_SERIAL`, `UNITY_EMAIL`,
`UNITY_PASSWORD`. The single-secret route is preferred: no account password
in CI.

**Once the repository is public, the workflow logs are world-readable.** The
gate report must never contain a licence file or a raw device serial. Use the
device *variant* (`SM-A546B`), not a serial.

---

## 7. Failure modes, and why 404 becomes 403

| Code | What it means here | What to do |
|---|---|---|
| **401** | No credential, or a bad/expired one | Check the token exists and has not expired |
| **403** | Credential is valid but **lacks the permission** | For Actions: add `permissions: contents: write`. For a PAT: check `Contents: Read and write` |
| **404** | The resource is invisible to you | On a **private** repo, a 404 usually means "no access" — GitHub hides existence. On a **public** repo the same missing access surfaces as **403**, because the resource is known to exist |

That last row is the one that confuses people. The same missing permission
produces a different code depending on visibility:

- **Private repo, no access** → `404 Not Found` (GitHub will not confirm the
  repository exists)
- **Public repo, no write access** → `403 Forbidden` (the repository is
  visible, so GitHub can say "you may not write to it")

This repository is now public, so **expect 403, not 404**, when a write is
refused. A 404 here now means the *path* is wrong, not the permission.

---

## 8. Traps that silently drop or corrupt an agent's files

### 8.1 Git LFS is not installed in the sandbox

```
$ which git-lfs
git-lfs NOT INSTALLED
$ git lfs version
git: 'lfs' is not a git command.
```

`.gitattributes` routes **37 patterns** to LFS, including `*.png`, `*.jpg`,
`*.fbx`, `*.psd`, `*.exr` and `*.wav`. The pattern is by **extension, not by
directory**:

```
$ git check-attr filter -- docs/diagram.png
docs/diagram.png: lfs
```

So a PNG anywhere in the repository — including under `docs/` — is supposed
to be an LFS object. With `git-lfs` absent, `git add` writes the **raw bytes
as a normal blob** instead. The file looks correct locally and is broken for
everyone else: GitHub serves the pointer text, not the image.

**The API path has the same problem.** `GITHUB_CREATE_OR_UPDATE_FILE_CONTENTS`
always writes a regular blob; it has no LFS support. A PNG committed through
the API is a non-LFS blob even though `.gitattributes` says otherwise.

**What to do:** for binary assets, either install `git-lfs` in the sandbox
first (`apt-get install -y git-lfs && git lfs install`), or commit the asset
from a machine that has it. Text files — `.cs`, `.md`, `.json`, `.yml`,
`.sh`, `.py` — are unaffected and are the overwhelming majority of what an
agent writes here.

### 8.2 `.gitignore` rules that will silently drop a file

| Rule | Effect |
|---|---|
| `/BenchmarkResults/*` | Everything in that directory is ignored **except** `verdict_*.txt` and `.gitkeep` |
| `*.ulf`, `*.ulm` | Unity licence files are ignored — **correct**, keep it that way |
| `/_SolverScratch/`, `/_ArtStaging/` | Local scratch, ignored by design |
| `/TierOverride.local.json` | Machine-specific override, ignored by design |
| `*.csproj`, `*.sln`, `*.user` | IDE state, ignored by design |

If an agent writes a file and it does not appear in `git status`, check
`.gitignore` before assuming the write failed.

### 8.3 Nothing blocks a push

Verified on 2026-10-03:

| Check | Result |
|---|---|
| Branch protection on `main` | **none** (404) |
| Rulesets | **none** (`{"rulesets": []}`) |
| Required status checks | none |
| Required signed commits | none (`web_commit_signoff_required: false`) |
| Required reviews | none |
| Deploy keys | none |
| Actions secrets / variables | 0 / 0 |
| Default workflow permissions | `read` |
| Can Actions approve PRs | `false` |
| Allowed actions | all allowed (409 = policy is `all`) |

`main` is unprotected, so any credential with write access can push to it
directly. That is convenient for an agent and is also the reason a leaked
credential is dangerous — there is no second line of defence.

---

## 9. Recommended setup

For an agent that needs to write files, in order of preference:

1. **The GitHub API tools** — already working, no credential to manage, no
   token to leak. Use this unless you specifically need `git` semantics
   (branches, merges, multi-file atomic commits).
2. **A fine-grained PAT scoped to this one repository, `Contents: Read and
   write`** — if you need real `git`. Store it as a secret. Rotate it if it
   is ever exposed.
3. **A deploy key with write access** — if you prefer a key over a token and
   want the credential hard-scoped to this one repository.
4. **`GITHUB_TOKEN` in a workflow** — only for automation that runs *inside*
   Actions, and only with an explicit `permissions: contents: write`.

---

## 10. What was not tested, and why

| Path | Why not |
|---|---|
| PAT over HTTPS | No PAT exists in this environment, and an agent cannot mint one. Only the owner can. |
| Deploy key over SSH | No keypair exists, and an agent cannot add one without the owner's public key. |
| A real gate run | No `UNITY_LICENSE` secret exists, so the gate job is skipped. **Nothing in this repository has ever been compiled.** |

Everything in sections 2, 3, 6 and 8 was executed and its output recorded.
Sections 4 and 5 describe paths that are **untested** — the commands are the
standard ones, but they have not been run against this repository.
