import { ArrowUp, Bot, LoaderCircle } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";

import { getHealth, getSessions, sendPrompt } from "./api";

const WELCOME_MESSAGE = {
  id: "welcome",
  role: "assistant",
  text: "Опишите геометрию, которую нужно создать или изменить в Rhino."
};

export function App() {
  const [sessionId, setSessionId] = useState("");
  const [rhinoVersion, setRhinoVersion] = useState("");
  const [backendOnline, setBackendOnline] = useState(false);
  const [messages, setMessages] = useState([WELCOME_MESSAGE]);
  const [prompt, setPrompt] = useState("");
  const [busy, setBusy] = useState(false);
  const messagesEndRef = useRef(null);

  const refreshConnection = useCallback(async (signal) => {
    try {
      await getHealth(signal);
      const sessions = await getSessions(signal);
      const active = sessions[0];
      setBackendOnline(true);
      setSessionId(active?.session_id ?? "");
      setRhinoVersion(active?.rhino_version ?? "");
    } catch (error) {
      if (error.name === "AbortError") return;
      setBackendOnline(false);
      setSessionId("");
      setRhinoVersion("");
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    refreshConnection(controller.signal);
    const timer = window.setInterval(() => refreshConnection(controller.signal), 3000);
    return () => {
      controller.abort();
      window.clearInterval(timer);
    };
  }, [refreshConnection]);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, busy]);

  const submitPrompt = async (event) => {
    event.preventDefault();
    const text = prompt.trim();
    if (!text || !sessionId || busy) return;

    setPrompt("");
    setBusy(true);
    setMessages((current) => [
      ...current,
      { id: crypto.randomUUID(), role: "user", text }
    ]);

    try {
      const result = await sendPrompt(sessionId, text);
      const count = result.plan.operations.length;
      const suffix = count ? ` Выполнено операций: ${count}.` : "";
      setMessages((current) => [
        ...current,
        { id: crypto.randomUUID(), role: "assistant", text: `${result.message}${suffix}` }
      ]);
    } catch (error) {
      setMessages((current) => [
        ...current,
        { id: crypto.randomUUID(), role: "error", text: error.message }
      ]);
    } finally {
      setBusy(false);
    }
  };

  const handleKeyDown = (event) => {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      event.currentTarget.form?.requestSubmit();
    }
  };

  const connected = Boolean(sessionId);
  const statusText = connected
    ? `Rhino ${rhinoVersion || "8"}`
    : backendOnline
      ? "Ожидание Rhino"
      : "Нет соединения";

  return (
    <main className="chat-app">
      <header className="chat-header">
        <span className="app-mark"><Bot size={18} /></span>
        <div className="app-identity">
          <strong>RHINO Minion</strong>
          <span>{statusText}</span>
        </div>
        <span className={`connection-dot ${connected ? "online" : ""}`} />
      </header>

      <section className="messages" aria-live="polite">
        {messages.map((message) => (
          <div className={`message-row ${message.role}`} key={message.id}>
            {message.role !== "user" && <span className="message-avatar"><Bot size={14} /></span>}
            <div className="message-bubble">{message.text}</div>
          </div>
        ))}
        {busy && (
          <div className="message-row assistant">
            <span className="message-avatar"><Bot size={14} /></span>
            <div className="message-bubble typing">
              <LoaderCircle size={15} className="spin" />
              Создаю геометрию…
            </div>
          </div>
        )}
        <div ref={messagesEndRef} />
      </section>

      <form className="composer" onSubmit={submitPrompt}>
        <textarea
          aria-label="Сообщение"
          disabled={!connected || busy}
          onChange={(event) => setPrompt(event.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={connected ? "Напишите, что создать…" : statusText}
          rows="1"
          value={prompt}
        />
        <button
          aria-label="Отправить"
          disabled={!connected || busy || !prompt.trim()}
          type="submit"
        >
          {busy ? <LoaderCircle size={17} className="spin" /> : <ArrowUp size={18} />}
        </button>
      </form>
    </main>
  );
}
