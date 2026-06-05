export default function LanguageToggle({ lang, setLang }) {
  return (
    <div className="lang-toggle">
      <button type="button" className={lang === 'tr' ? 'active' : ''} onClick={() => setLang('tr')}>
        TR
      </button>
      <button type="button" className={lang === 'en' ? 'active' : ''} onClick={() => setLang('en')}>
        EN
      </button>
    </div>
  )
}
