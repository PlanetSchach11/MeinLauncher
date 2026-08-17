import { useInView } from '../hooks/useInView';

export default function News() {
  const { ref: titleRef, isVisible: titleVisible } = useInView({ threshold: 0.2 });
  const { ref: emptyRef, isVisible: emptyVisible } = useInView({ threshold: 0.1 });

  return (
    <section className="section" id="news" style={{ background: 'var(--bg-elevated)' }}>
      <div className="container">
        <div ref={titleRef} className={`reveal${titleVisible ? ' visible' : ''}`}>
          <span className="section-label">News</span>
          <h2 className="section-title">Neuigkeiten</h2>
          <p className="section-subtitle">
            Bleib über Updates und neue Funktionen informiert.
          </p>
        </div>

        <div
          ref={emptyRef}
          className={`news-empty reveal${emptyVisible ? ' visible' : ''}`}
        >
          <div className="news-empty-icon" aria-hidden="true">📋</div>
          <h3 className="news-empty-title">Noch keine Neuigkeiten</h3>
          <p className="news-empty-text">
            Hier erscheinen zukünftig Updates, neue Funktionen und wichtige Ankündigungen.
          </p>
          <p className="news-empty-hint">Die ersten Neuigkeiten folgen bald.</p>
        </div>
      </div>
    </section>
  );
}
