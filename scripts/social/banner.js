// Sizes the banner from the query string, for example banner.html?w=1280&h=640.
const params = new URLSearchParams(location.search);
for (const [param, property] of [['w', '--w'], ['h', '--h']]) {
  const value = Number(params.get(param));
  if (value > 0) document.documentElement.style.setProperty(property, value + 'px');
}
