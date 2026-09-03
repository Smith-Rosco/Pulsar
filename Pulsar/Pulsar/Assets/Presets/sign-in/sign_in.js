javascript:(function(){
  // Pulsar Office Action Preset - Sign-In Flow
  // Brings the first credential field into focus so a password manager / PKI fill can run.
  var fields = document.querySelectorAll('input[type=password], input[name*=user], input[name*=login], input[name*=email]');
  if (fields.length === 0) return;
  fields[0].focus();
})();
