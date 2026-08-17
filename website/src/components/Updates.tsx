import { useInView } from '../hooks/useInView';

const STEPS = [
  { text: '<strong>GitHub Actions</strong> baut und testet jede Änderung automatisch.' },
  { text: 'Ein neues <strong>GitHub Release</strong> wird mit der neuen Version erstellt.' },
  { text: 'Kulka Client erkennt das Update beim Start und lädt die neue Version herunter.' },
  { text: 'Der <strong>Updater</strong> installiert die neue Version – ohne manuelles Eingreifen.' },
];

const INFO_ITEMS = [
  { icon: '🔒', text: '<strong>Sicher:</strong> Alle Updates werden über geprüfte GitHub Releases ausgeliefert.' },
  { icon: '⚡', text: '<strong>Schnell:</strong> Nur geänderte Dateien werden heruntergeladen.' },
  { icon: '🔄', text: '<strong>Optional:</strong> Du entscheidest, wann du updatest.' },
];

export default function Updates() {
  const { ref, isVisible } = useInView({ threshold: 0.1 });

  return (
    <section className="section" id="updates">
      <div className="container" ref={ref}>
        <div className={`reveal${isVisible ? ' visible' : ''}`}>
          <span className="section-label">Updates</span>
          <h2 className="section-title">Kulka Client bleibt aktuell</h2>
          <p className="section-subtitle">
            Neue Versionen werden automatisch erkannt und können mit einem Klick installiert werden.
          </p>
        </div>

        <div className="updates-layout">
          <div className={`reveal-left${isVisible ? ' visible' : ''}`} style={{ transitionDelay: '150ms' }}>
            <div className="updates-visual">
              <div className="update-version-badge">
                <span aria-hidden="true">📦</span> Coming Soon
              </div>
              <div className="update-timeline">
                {STEPS.map((step, i) => (
                  <div key={i} className="update-step">
                    <div className="update-step-dot" />
                    <p className="update-step-text" dangerouslySetInnerHTML={{ __html: step.text }} />
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div className={`reveal-right${isVisible ? ' visible' : ''}`} style={{ transitionDelay: '300ms' }}>
            <div className="updates-info">
              {INFO_ITEMS.map((item, i) => (
                <div key={i} className="update-info-item">
                  <span className="update-info-icon" aria-hidden="true">{item.icon}</span>
                  <p className="update-info-text" dangerouslySetInnerHTML={{ __html: item.text }} />
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
