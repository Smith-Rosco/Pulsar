javascript:(function(){
  // Pulsar Office Action Preset - Common Form Fill
  // Focuses the first text input on the current page and fills sample data.
  var inputs = document.querySelectorAll('input[type=text], input:not([type])');
  if (inputs.length === 0) return;
  var target = inputs[0];
  target.focus();
  target.value = 'Pulsar';
  target.dispatchEvent(new Event('input', { bubbles: true }));
})();
