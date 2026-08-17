import { useState, useEffect } from 'react';

const NAV_ITEMS = [
  { label: 'Home', href: '#home' },
  { label: 'Features', href: '#features' },
  { label: 'Download', href: '#download' },
  { label: 'News', href: '#news' },
  { label: 'Changelog', href: '#changelog' },
  { label: 'Support', href: '#support' },
];

export default function Navigation() {
  const [scrolled, setScrolled] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 20);
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  return (
    <nav
      className={`nav${scrolled ? ' scrolled' : ''}`}
      role="navigation"
      aria-label="Hauptnavigation"
    >
      <div className="nav-inner">
        <a href="#home" className="nav-brand" aria-label="Kulka Client – Startseite">
          <span className="nav-brand-icon" aria-hidden="true">K</span>
          <span>Kulka Client</span>
        </a>

        <div className={`nav-links${mobileOpen ? ' open' : ''}`}>
          {NAV_ITEMS.map((item) => (
            <a
              key={item.href}
              href={item.href}
              className="nav-link"
              onClick={() => setMobileOpen(false)}
            >
              {item.label}
            </a>
          ))}
          <a href="#download" className="btn btn-primary btn-sm nav-cta" onClick={() => setMobileOpen(false)}>
            Download
          </a>
        </div>

        <button
          className="nav-mobile-toggle"
          onClick={() => setMobileOpen(!mobileOpen)}
          aria-label={mobileOpen ? 'Menü schließen' : 'Menü öffnen'}
          aria-expanded={mobileOpen}
        >
          {mobileOpen ? '✕' : '☰'}
        </button>
      </div>
    </nav>
  );
}
