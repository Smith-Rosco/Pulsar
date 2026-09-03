javascript:(function(){
  // Extract rows from the first data table on a legacy page into the console.
  var table = document.querySelector('table');
  if (!table) {
    alert('No table found on this page.');
    return;
  }
  var rows = Array.prototype.slice.call(table.querySelectorAll('tr'));
  var data = rows.map(function(row) {
    return Array.prototype.slice.call(row.querySelectorAll('td,th'))
      .map(function(cell) { return cell.innerText.trim(); })
      .join(' | ');
  });
  console.log('[Pulsar] Extracted table:');
  console.log(data.join('\n'));
  alert('Extracted ' + data.length + ' row(s). See console for details.');
})();
