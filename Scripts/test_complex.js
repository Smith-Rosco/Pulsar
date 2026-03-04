// Complex bookmarklet with modern formatting
// This tests NUglify's ability to handle:
// - Multi-line code
// - Comments
// - Template literals
// - Arrow functions
// - Const/let declarations

(function () {
  // Configuration
  const config = {
    highlightColor: 'yellow',
    borderColor: 'red',
    borderWidth: '2px'
  };

  /* 
   * Find all links on the page
   * and apply highlighting
   */
  const links = document.querySelectorAll('a');
  
  // Apply styles to each link
  links.forEach((link) => {
    link.style.backgroundColor = config.highlightColor;
    link.style.border = `${config.borderWidth} solid ${config.borderColor}`;
  });

  // Show result
  alert(`Highlighted ${links.length} links on: ${document.title}`);
})();
