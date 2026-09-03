javascript:(function(){
  // Collect links on a legacy page and navigate to the first external one.
  var links = Array.prototype.slice.call(document.querySelectorAll('a[href]'));
  if (!links.length) {
    alert('No links found on this page.');
    return;
  }
  var external = links.filter(function(a) { return /^https?:/i.test(a.href); });
  var target = external[0] || links[0];
  window.location.href = target.href;
})();
