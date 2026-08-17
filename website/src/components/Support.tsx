import { useInView } from '../hooks/useInView';

const SUPPORT_ITEMS = [
  {
    icon: '🐛',
    title: 'Fehler melden',
    desc: 'Du hast einen Bug gefunden? Eröffne ein Issue auf GitHub und hilf mit, den Kulka Client zu verbessern.',
  },
  {
    icon: '💬',
    title: 'Hilfe & Fragen',
    desc: 'Hast du Fragen zur Nutzung? Schau in die Dokumentation oder erstelle eine Discussion auf GitHub.',
  },
  {
    icon: '📖',
    title: 'FAQ',
    desc: 'Antworten auf häufig gestellte Fragen findest du in unserer Online-Dokumentation.',
  },
];

export default function Support() {
  const { ref: titleRef, isVisible: titleVisible } = useInView({ threshold: 0.2 });
  const { ref: gridRef, isVisible: gridVisible } = useInView({ threshold: 0.1 });

  return (
    <section className="section" id="support" style={{ background: 'var(--bg-elevated)' }}>
      <div className="container">
        <div ref={titleRef} className={`reveal${titleVisible ? ' visible' : ''}`} style={{ textAlign: 'center' }}>
          <span className="section-label" style={{ justifyContent: 'center' }}>Support</span>
          <h2 className="section-title" style={{ textAlign: 'center' }}>
            Hilfe benötigt?
          </h2>
          <p className="section-subtitle" style={{ textAlign: 'center', margin: '0 auto' }}>
            Wir helfen dir gerne weiter. Wähle die passende Option.
          </p>
        </div>

        <div
          ref={gridRef}
          className={`support-grid stagger-children${gridVisible ? ' visible' : ''}`}
        >
          {SUPPORT_ITEMS.map((item) => (
            <div key={item.title} className="card support-card">
              <div className="support-icon" aria-hidden="true">{item.icon}</div>
              <h3 className="support-title">{item.title}</h3>
              <p className="support-desc">{item.desc}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
