javascript:(function(){
  // Fill common form fields on a legacy page with sample values.
  // Replace the sample value with your own before using on a real page.
  var inputs = document.querySelectorAll('input[type=text], input[type=password], input:not([type])');
  var filled = 0;
  for (var i = 0; i < inputs.length && filled < 3; i++) {
    var el = inputs[i];
    if (el.offsetParent === null) continue; // skip hidden fields
    el.value = 'Pulsar Demo';
    filled++;
  }
  alert('Filled ' + filled + ' field(s) with sample values.');
})();
