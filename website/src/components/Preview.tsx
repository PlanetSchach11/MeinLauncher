import { useInView } from '../hooks/useInView';

const SCREENSHOTS = [
  {
    src: '/screenshots/startseite.png',
    alt: 'Kulka Client Startseite – Profilübersicht mit Sidebar, Profilkarten und Start-Button',
    label: 'Startseite',
  },
  {
    src: '/screenshots/profile.png',
    alt: 'Kulka Client Profilverwaltung – Detailansicht eines Spielprofils mit Mods und Einstellungen',
    label: 'Profile',
  },
  {
    src: '/screenshots/einstellungen.png',
    alt: 'Kulka Client Einstellungen – Theme, Sprache, Java-Runtime und Speicher konfigurieren',
    label: 'Einstellungen',
  },
  {
    src: '/screenshots/news.png',
    alt: 'Kulka Client News – Neuigkeiten und Updates direkt im Launcher',
    label: 'News',
  },
];

export default function Preview() {
  const { ref: titleRef, isVisible: titleVisible } = useInView({ threshold: 0.2 });
  const { ref: galleryRef, isVisible: galleryVisible } = useInView({ threshold: 0.05 });

  return (
    <section className="section" id="preview">
      <div className="container">
        <div ref={titleRef} className={`reveal${titleVisible ? ' visible' : ''}`} style={{ textAlign: 'center' }}>
          <span className="section-label" style={{ justifyContent: 'center' }}>Launcher</span>
          <h2 className="section-title" style={{ textAlign: 'center', margin: '0 auto 16px' }}>
            So sieht Kulka Client aus
          </h2>
          <p className="section-subtitle" style={{ textAlign: 'center', margin: '0 auto' }}>
            Eine moderne, aufgeräumte Oberfläche – entwickelt für Geschwindigkeit und Übersicht.
          </p>
        </div>

        <div
          ref={galleryRef}
          className={`screenshot-gallery stagger-children${galleryVisible ? ' visible' : ''}`}
        >
          {SCREENSHOTS.map((shot) => (
            <figure key={shot.src} className="screenshot-item">
              <div className="screenshot-window">
                <div className="preview-titlebar">
                  <span className="preview-dot" />
                  <span className="preview-dot" />
                  <span className="preview-dot" />
                  <span className="preview-titlebar-text">Kulka Client</span>
                </div>
                <div className="preview-body preview-body--gallery">
                  <img
                    src={shot.src}
                    alt={shot.alt}
                    className="preview-screenshot"
                    width="760"
                    height="480"
                    loading="lazy"
                  />
                </div>
              </div>
              <figcaption className="screenshot-label">{shot.label}</figcaption>
            </figure>
          ))}
        </div>
      </div>
    </section>
  );
}
