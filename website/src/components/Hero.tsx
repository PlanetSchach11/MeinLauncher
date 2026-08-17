import { useInView } from '../hooks/useInView';

export default function Hero() {
  const { ref, isVisible } = useInView({ threshold: 0.1, triggerOnce: true });

  return (
    <section className="hero" id="home">
      <div className="hero-bg" aria-hidden="true">
        <div className="hero-gradient" />
        <div className="hero-gradient-2" />
      </div>

      <div className="container">
        <div className="hero-content" ref={ref}>
          <div className={`hero-text reveal${isVisible ? ' visible' : ''}`}>
            <div className="hero-badge">
              <span className="hero-badge-dot" aria-hidden="true" />
              v0.1.0
            </div>

            <h1 className="hero-title">
              Minecraft.<br />
              <span className="hero-title-accent">Einfach besser</span> verwaltet.
            </h1>

            <p className="hero-description">
              Kulka Client ist dein moderner Minecraft Launcher für Windows.
              Verwalte Profile, Mods und Einstellungen – alles an einem Ort.
            </p>

            <div className="hero-actions">
              <a href="#download" className="btn btn-primary">
                <span aria-hidden="true">⬇</span>
                Für Windows herunterladen
              </a>
              <a href="#features" className="btn btn-secondary">
                Mehr erfahren
              </a>
            </div>

            <p className="hero-note">Kostenlos · Keine Anmeldung nötig · Windows 10/11</p>
          </div>

          <div
            className={`hero-visual reveal-scale${isVisible ? ' visible' : ''}`}
            style={{ transitionDelay: '200ms' }}
          >
            <div className="preview-wrapper">
              <div className="preview-screen">
                <div className="preview-titlebar">
                  <span className="preview-dot" />
                  <span className="preview-dot" />
                  <span className="preview-dot" />
                  <span className="preview-titlebar-text">Kulka Client</span>
                </div>
                <div className="preview-body preview-body--hero">
                  <img
                    src="/screenshots/startseite.png"
                    alt="Kulka Client Startseite – Profilübersicht mit Sidebar, Profilkarten und Start-Button"
                    className="preview-screenshot"
                    width="760"
                    height="480"
                    loading="eager"
                  />
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
