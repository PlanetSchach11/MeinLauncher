import { useInView } from '../hooks/useInView';

const ITEMS = [
  {
    number: '01',
    title: 'Versionen',
    desc: 'Mehrere Minecraft-Versionen installiert? Wechsle mit einem Klick.',
  },
  {
    number: '02',
    title: 'Profile',
    desc: 'Verschiedene Spielstände mit eigenen Mods, Einstellungen und Speicherständen.',
  },
  {
    number: '03',
    title: 'Mods & Texture Packs',
    desc: 'Direkt über Modrinth suchen und installieren – kein manuelles Kopieren mehr.',
  },
  {
    number: '04',
    title: 'Einstellungen',
    desc: 'Java-Pfad, RAM, Theme, Sprache und Hintergrund – alles zentral konfigurierbar.',
  },
];

export default function AllInOne() {
  const { ref: titleRef, isVisible: titleVisible } = useInView({ threshold: 0.2 });
  const { ref: gridRef, isVisible: gridVisible } = useInView({ threshold: 0.05 });

  return (
    <section className="section" style={{ background: 'var(--bg-elevated)' }}>
      <div className="container">
        <div ref={titleRef} className={`reveal${titleVisible ? ' visible' : ''}`}>
          <span className="section-label">Übersicht</span>
          <h2 className="section-title">Alles an einem Ort</h2>
          <p className="section-subtitle">
            Kein Wechsel zwischen verschiedenen Tools mehr. Kulka Client bündelt die komplette Minecraft-Verwaltung.
          </p>
        </div>

        <div
          ref={gridRef}
          className={`allinone-grid stagger-children${gridVisible ? ' visible' : ''}`}
        >
          {ITEMS.map((item) => (
            <div key={item.number} className="card allinone-item">
              <div className="allinone-number" aria-hidden="true">{item.number}</div>
              <h3 className="allinone-title">{item.title}</h3>
              <p className="allinone-desc">{item.desc}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
