import { useInView } from '../hooks/useInView';

const FEATURES = [
  {
    icon: '👤',
    title: 'Profile',
    desc: 'Erstelle individuelle Spielprofile mit eigener Version, Mods und Einstellungen.',
  },
  {
    icon: '🎮',
    title: 'Minecraft-Versionen',
    desc: 'Installiere und wechsle zwischen verschiedenen Minecraft-Versionen.',
  },
  {
    icon: '🧩',
    title: 'Mods',
    desc: 'Suche, installiere und verwalte Mods direkt über Modrinth.',
  },
  {
    icon: '🔄',
    title: 'Automatische Updates',
    desc: 'Bleibt automatisch auf dem neuesten Stand – kein manuelles Update nötig.',
  },
  {
    icon: '⚙️',
    title: 'Einstellungen',
    desc: 'Theme, Sprache, Java-Runtime, RAM und weitere Anpassungsoptionen.',
  },
  {
    icon: '🔐',
    title: 'Microsoft Account',
    desc: 'Sichere Anmeldung mit deinem Microsoft-Konto für Premium-Inhalte.',
  },
];

export default function Features() {
  const { ref: titleRef, isVisible: titleVisible } = useInView({ threshold: 0.2 });
  const { ref: gridRef, isVisible: gridVisible } = useInView({ threshold: 0.05 });

  return (
    <section className="section" id="features">
      <div className="container">
        <div ref={titleRef} className={`reveal${titleVisible ? ' visible' : ''}`}>
          <span className="section-label">Features</span>
          <h2 className="section-title">Alles was du brauchst</h2>
          <p className="section-subtitle">
            Kulka Client vereint die wichtigsten Funktionen in einer Anwendung – schnell, übersichtlich und zuverlässig.
          </p>
        </div>

        <div
          ref={gridRef}
          className={`features-grid stagger-children${gridVisible ? ' visible' : ''}`}
        >
          {FEATURES.map((f) => (
            <div key={f.title} className="card feature-card">
              <div className="feature-icon" aria-hidden="true">{f.icon}</div>
              <h3 className="feature-title">{f.title}</h3>
              <p className="feature-desc">{f.desc}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
