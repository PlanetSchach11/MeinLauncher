import { useInView } from '../hooks/useInView';

const CHANGELOG = [
  {
    version: 'v1.0.0',
    date: '23. August 2026',
    categories: [
      {
        label: 'NEW',
        type: 'new' as const,
        items: [
          'Kulka Client Mod (kulka-client-1.0.0.jar) — wird automatisch bei jedem Start deployed',
          'Kulka Client Integration für Cross-Server-Spielererkennung',
          'FPS-Zähler im HUD',
          'Periodischer News-Check alle 5 Minuten mit rotem Punkt',
        ],
      },
      {
        label: 'IMPROVED',
        type: 'improved' as const,
        items: [
          'Vollständige Lokalisierung (Deutsch/Englisch) für alle UI-Strings',
          'Performance-Verbesserungen im Hintergrundrenderer (~93% weniger Fills)',
          'Desktop-Symbol aktualisiert',
        ],
      },
      {
        label: 'FIXED',
        type: 'fixed' as const,
        items: [
          'Debug-Texte und Prototyp-Markierung entfernt',
          'Cross-Thread-Sicherheit für Thumbnail-Laden und Unread-Status',
          'Token-Leck in Debug-Logs behoben',
        ],
      },
    ],
  },
  {
    version: 'v0.1.0',
    date: 'Platzhalter – Veröffentlichungsdatum',
    categories: [
      {
        label: 'NEW',
        type: 'new' as const,
        items: [
          'Initiale Veröffentlichung des Kulka Client',
          'Profil-System mit individuellen Einstellungen',
          'Modrinth-Integration für Mod-Suche und Installation',
          'Microsoft-Konto-Anmeldung',
          'News-Bereich mit YouTube-Integration',
        ],
      },
      {
        label: 'IMPROVED',
        type: 'improved' as const,
        items: [
          'Moderne, dunkle Benutzeroberfläche',
          'Responsive Design für verschiedene Bildschirmgrößen',
        ],
      },
      {
        label: 'FIXED',
        type: 'fixed' as const,
        items: [
          'Einstellungen bleiben nach Navigation erhalten',
        ],
      },
    ],
  },
];

export default function Changelog() {
  const { ref: titleRef, isVisible: titleVisible } = useInView({ threshold: 0.2 });
  const { ref: listRef, isVisible: listVisible } = useInView({ threshold: 0.05 });

  return (
    <section className="section" id="changelog">
      <div className="container">
        <div ref={titleRef} className={`reveal${titleVisible ? ' visible' : ''}`}>
          <span className="section-label">Changelog</span>
          <h2 className="section-title">Was hat sich geändert</h2>
          <p className="section-subtitle">
            Alle Änderungen und Updates des Kulka Client chronologisch dokumentiert.
          </p>
        </div>

        <div
          ref={listRef}
          className="changelog-list"
        >
          {CHANGELOG.map((entry) => (
            <div key={entry.version} className={`changelog-entry reveal${listVisible ? ' visible' : ''}`}>
              <div className="changelog-version">{entry.version}</div>
              <div className="changelog-date">{entry.date}</div>
              {entry.categories.map((cat) => (
                <div key={cat.label} className="changelog-category">
                  <span className={`changelog-category-label ${cat.type}`}>
                    {cat.label}
                  </span>
                  <div className="changelog-items">
                    {cat.items.map((item, j) => (
                      <div key={j} className="changelog-item">{item}</div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
