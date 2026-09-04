import {
  ArrowUpRight,
  Box,
  CircleDot,
  Command,
  Layers3,
  MessageSquareText,
  MousePointer2,
  ScanLine,
  Sparkles,
  RefreshCw,
  AlertCircle,
  CheckCircle2
} from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { executeRhinoCommand, getHealth, getSessions } from "./api";

function RhinoMark({ compact = false }) {
  return (
    <svg
      className={compact ? "rhino-mark compact" : "rhino-mark"}
      viewBox="0 0 160 110"
      aria-label="RHINO_minion logo"
      role="img"
    >
      <path
        d="M24 65V39l17-14 31 9 20 17 27 8 18 21-15 13-41 7-31-7-26-28Z"
        fill="none"
        stroke="currentColor"
        strokeWidth="6"
        strokeLinejoin="round"
      />
      <path
        d="M91 51 121 21l-10 42"
        fill="#d9dce1"
        stroke="currentColor"
        strokeWidth="5"
        strokeLinejoin="round"
      />
      <rect x="54" y="53" width="36" height="20" rx="10" fill="currentColor" />
      <circle cx="66" cy="63" r="3.5" fill="#f7f7f5" />
      <circle cx="77" cy="63" r="3.5" fill="#d9dce1" />
      <circle cx="29" cy="55" r="11" fill="#f7f7f5" stroke="currentColor" strokeWidth="5" />
      <path d="M34 32 34 15l18 8" fill="none" stroke="currentColor" strokeWidth="6" strokeLinejoin="round"/>
    </svg>
  );
}

const actions = [
  { icon: Box, label: "Create geometry" },
  { icon: Layers3, label: "Organize layers" },
  { icon: ScanLine, label: "Read viewport" },
  { icon: Command, label: "Run operations" }
];

const DEMO_PROMPT = "Create a box 30 × 20 × 80 m";

function summarizeScene(scene) {
  const count = scene?.objects?.length ?? 0;
  const units = scene?.units ?? "unknown units";
  return `${count} object${count === 1 ? "" : "s"} · ${units}`;
}

function metersToDocumentUnits(units) {
  const factors = {
    Millimeters: 1000,
    Centimeters: 100,
    Decimeters: 10,
    Meters: 1,
    Inches: 39.3700787402,
    Feet: 3.280839895
  };
  return factors[units] ?? null;
}

export function App() {
  const [backendOnline, setBackendOnline] = useState(false);
  const [sessions, setSessions] = useState([]);
  const [sessionId, setSessionId] = useState("");
  const [scene, setScene] = useState(null);
  const [prompt, setPrompt] = useState(DEMO_PROMPT);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState({ type: "idle", message: "Waiting for Rhino" });

  const activeSession = useMemo(
    () => sessions.find((session) => session.session_id === sessionId),
    [sessions, sessionId]
  );

  const refreshConnections = useCallback(async (signal) => {
    try {
      await getHealth(signal);
      const nextSessions = await getSessions(signal);
      setBackendOnline(true);
      setSessions(nextSessions);
      setSessionId((current) => {
        if (nextSessions.some((session) => session.session_id === current)) return current;
        return nextSessions[0]?.session_id ?? "";
      });
    } catch (error) {
      if (error.name === "AbortError") return;
      setBackendOnline(false);
      setSessions([]);
      setSessionId("");
      setScene(null);
      setNotice({ type: "error", message: "Backend is offline" });
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    refreshConnections(controller.signal);
    const timer = window.setInterval(() => refreshConnections(controller.signal), 3000);
    return () => {
      controller.abort();
      window.clearInterval(timer);
    };
  }, [refreshConnections]);

  useEffect(() => {
    if (!sessionId) {
      setNotice({
        type: backendOnline ? "idle" : "error",
        message: backendOnline ? "Open Rhino and load the plugin" : "Backend is offline"
      });
      return;
    }
    setNotice({ type: "success", message: `Rhino ${activeSession?.rhino_version ?? "8"} connected` });
  }, [activeSession, backendOnline, sessionId]);

  const readScene = useCallback(async () => {
    if (!sessionId) return;
    setBusy(true);
    try {
      const response = await executeRhinoCommand(sessionId, "get_scene", { max_objects: 500 });
      if (!response.ok) throw new Error(response.error?.message ?? "Could not read scene");
      setScene(response.result);
      setNotice({ type: "success", message: `Scene updated · ${summarizeScene(response.result)}` });
    } catch (error) {
      setNotice({ type: "error", message: error.message });
    } finally {
      setBusy(false);
    }
  }, [sessionId]);

  const createDemoBox = useCallback(async () => {
    if (!sessionId || busy) return;
    setBusy(true);
    setNotice({ type: "working", message: "Creating geometry in Rhino…" });
    try {
      const sceneResponse = await executeRhinoCommand(sessionId, "get_scene", { max_objects: 500 });
      if (!sceneResponse.ok) throw new Error(sceneResponse.error?.message ?? "Could not read document units");
      const unitFactor = metersToDocumentUnits(sceneResponse.result.units);
      if (unitFactor === null) {
        throw new Error(`Demo does not support Rhino units: ${sceneResponse.result.units}`);
      }
      const response = await executeRhinoCommand(sessionId, "execute_batch", {
        label: "RHINO Minion: demo box",
        operations: [
          {
            operation_id: crypto.randomUUID(),
            tool: "create_box",
            arguments: {
              origin: [0, 0, 0],
              width: 30 * unitFactor,
              depth: 20 * unitFactor,
              height: 80 * unitFactor,
              layer: "AI_Massing",
              name: "Tower_Base"
            }
          }
        ]
      });
      if (!response.ok) throw new Error(response.error?.message ?? "Geometry operation failed");
      const updatedScene = await executeRhinoCommand(sessionId, "get_scene", { max_objects: 500 });
      if (updatedScene.ok) setScene(updatedScene.result);
      setNotice({ type: "success", message: "30 × 20 × 80 m mass created · Ctrl+Z to undo in Rhino" });
    } catch (error) {
      setNotice({ type: "error", message: error.message });
    } finally {
      setBusy(false);
    }
  }, [busy, sessionId]);

  const submitPrompt = (event) => {
    event.preventDefault();
    if (prompt.trim().toLowerCase() !== DEMO_PROMPT.toLowerCase()) {
      setNotice({ type: "idle", message: "Free-form AI prompts arrive in the next stage. Use the demo prompt for now." });
      return;
    }
    createDemoBox();
  };

  return (
    <main className="shell">
      <header className="topbar">
        <a className="brand" href="#">
          <span className="brand-icon"><RhinoMark compact /></span>
          <span>RHINO<span className="underscore">_</span>minion</span>
        </a>

        <nav className="nav">
          <a href="#product">Product</a>
          <a href="#workflow">Workflow</a>
          <a href="#about">About</a>
        </nav>

        <button className="ghost-button" onClick={() => document.querySelector("#product")?.scrollIntoView()}>
          Launch app <ArrowUpRight size={16} />
        </button>
      </header>

      <section className="hero">
        <div className="hero-copy">
          <div className="eyebrow">
            <Sparkles size={14} />
            AI geometry assistant for Rhino
          </div>

          <h1>
            Describe it.
            <br />
            <span>Build it in Rhino.</span>
          </h1>

          <p className="lead">
            RHINO_minion turns natural language and visual references into
            editable Rhino geometry — directly inside your workflow.
          </p>

          <div className="hero-actions">
            <button className="primary-button" onClick={() => document.querySelector("#product")?.scrollIntoView()}>
              Start building
              <ArrowUpRight size={17} />
            </button>
            <button className="secondary-button" onClick={() => document.querySelector("#workflow")?.scrollIntoView()}>See workflow</button>
          </div>

          <div className="microcopy">
            <span><CircleDot size={14}/> Rhino 8</span>
            <span><CircleDot size={14}/> Grasshopper ready</span>
            <span><CircleDot size={14}/> Local bridge</span>
          </div>
        </div>

        <div className="workspace-card" id="product">
          <div className="workspace-top">
            <div className="workspace-title">
              <span className="mini-icon"><RhinoMark compact /></span>
              <div>
                <strong>RHINO_minion</strong>
                <small>{sessionId ? `Rhino ${activeSession?.rhino_version ?? "8"}` : "Waiting for Rhino"}</small>
              </div>
            </div>
            <span className={`status-dot ${sessionId ? "online" : ""}`} title={sessionId ? "Connected" : "Disconnected"} />
          </div>

          <div className="viewport">
            <div className="grid" />
            <div className="tower tower-a" />
            <div className="tower tower-b" />
            <div className="tower tower-c" />
            <div className="axis axis-x" />
            <div className="axis axis-y" />
            <div className="axis axis-z" />
            <div className="viewport-tag">
              <MousePointer2 size={13}/>
              Perspective
            </div>
          </div>

          <div className={`connection-panel ${notice.type}`}>
            <span className="connection-message">
              {notice.type === "error" ? <AlertCircle size={15} /> : notice.type === "success" ? <CheckCircle2 size={15} /> : <CircleDot size={15} />}
              {notice.message}
            </span>
            <div className="connection-actions">
              <select
                aria-label="Rhino session"
                value={sessionId}
                onChange={(event) => setSessionId(event.target.value)}
                disabled={!sessions.length || busy}
              >
                {!sessions.length && <option value="">No Rhino session</option>}
                {sessions.map((session) => (
                  <option key={session.session_id} value={session.session_id}>
                    Rhino {session.rhino_version} · {session.session_id.slice(0, 8)}
                  </option>
                ))}
              </select>
              <button className="icon-button" onClick={readScene} disabled={!sessionId || busy} title="Refresh scene">
                <RefreshCw size={15} className={busy ? "spin" : ""} />
              </button>
            </div>
          </div>

          <form className="prompt-card" onSubmit={submitPrompt}>
            <MessageSquareText size={18} />
            <textarea
              value={prompt}
              onChange={(event) => setPrompt(event.target.value)}
              aria-label="Geometry prompt"
              rows="2"
            />
            <button type="submit" aria-label="Run prompt" disabled={!sessionId || busy || !prompt.trim()}>
              {busy ? <RefreshCw size={18} className="spin" /> : <ArrowUpRight size={18}/>} 
            </button>
          </form>

          <div className="scene-summary">
            <span>{scene ? summarizeScene(scene) : "Scene not loaded"}</span>
            <button onClick={createDemoBox} disabled={!sessionId || busy}>Create demo mass</button>
          </div>
        </div>
      </section>

      <section className="feature-strip" id="workflow">
        {actions.map(({ icon: Icon, label }) => (
          <div className="feature" key={label}>
            <Icon size={18} />
            <span>{label}</span>
          </div>
        ))}
      </section>

      <section className="statement" id="about">
        <div>
          <span className="section-kicker">Minimal by design</span>
          <h2>Less interface. More geometry.</h2>
        </div>
        <p>
          A quiet, architecture-first interface built around one action:
          describe what you want, inspect the result, and iterate.
        </p>
      </section>
    </main>
  );
}
