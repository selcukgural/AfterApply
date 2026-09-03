// postman-collection requires '@faker-js/faker/locale/en' specifically, so that subpath has to
// resolve as a real file on disk.
//
// An "exports" map in package.json would express this more precisely, and that is what this stub
// did first — it worked on the Node 26 / npm 11 used locally and failed on CI's Node 22 / npm 10
// with "Cannot find module '@faker-js/faker/locale/en'", because subpath-exports resolution through
// an npm-linked `file:` override differs between those versions. A plain file needs no exports map
// and no resolver support beyond classic CommonJS, so it behaves the same everywhere.
module.exports = require('..');
