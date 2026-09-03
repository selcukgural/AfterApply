// Stand-in for @faker-js/faker, wired in through postman/package.json's `overrides`.
//
// Why this exists: postman-collection@5.3.1 pins @faker-js/faker at exactly 5.5.3, which carries a
// high-severity advisory (arbitrary code execution via helpers.fake). The advisory is fixed in
// 10.5.0+, but faker renamed `address` to `location` in v8 and postman-collection calls the old
// names eagerly, so forcing a patched version makes `npm run generate` die on
// `Cannot read properties of undefined (reading 'city')`. Both packages are already at their
// latest published versions, so there is no upgrade path — see DECISIONS.md 2026-09-02, where the
// risk was first accepted, and 2026-09-03, where it was closed this way instead.
//
// This is safe here because the only consumer is postman-collection's dynamic-variables.js, which
// resolves `{{$randomCity}}`-style placeholders at request time. Our generated collection contains
// none: scripts/generate-collection.js emits concrete example values from the OpenAPI schema. The
// module is still imported at load time, though, which is why a stub is needed rather than simply
// removing the dependency. Verified by regenerating collection.json and diffing it byte for byte.
//
// If a request ever does use a dynamic variable, it will render one of these obviously fake values
// rather than a plausible-looking random one — which is the intended failure mode: visible, not silent.
//
// Generated from the 111 call sites in postman-collection@5.3.1; regenerate if that version changes.

'use strict';

const stub = (name) => () => `stub:${name}`;

const faker = {
  address: {
    city: stub('address.city'),
    country: stub('address.country'),
    countryCode: stub('address.countryCode'),
    latitude: stub('address.latitude'),
    longitude: stub('address.longitude'),
    streetAddress: stub('address.streetAddress'),
    streetName: stub('address.streetName'),
  },
  commerce: {
    color: stub('commerce.color'),
    department: stub('commerce.department'),
    product: stub('commerce.product'),
    productAdjective: stub('commerce.productAdjective'),
    productMaterial: stub('commerce.productMaterial'),
    productName: stub('commerce.productName'),
  },
  company: {
    bs: stub('company.bs'),
    bsAdjective: stub('company.bsAdjective'),
    bsBuzz: stub('company.bsBuzz'),
    bsNoun: stub('company.bsNoun'),
    catchPhrase: stub('company.catchPhrase'),
    catchPhraseAdjective: stub('company.catchPhraseAdjective'),
    catchPhraseDescriptor: stub('company.catchPhraseDescriptor'),
    catchPhraseNoun: stub('company.catchPhraseNoun'),
    companyName: stub('company.companyName'),
    companySuffix: stub('company.companySuffix'),
  },
  database: {
    collation: stub('database.collation'),
    column: stub('database.column'),
    engine: stub('database.engine'),
    type: stub('database.type'),
  },
  datatype: {
    boolean: stub('datatype.boolean'),
    number: stub('datatype.number'),
    uuid: stub('datatype.uuid'),
  },
  date: {
    future: stub('date.future'),
    month: stub('date.month'),
    past: stub('date.past'),
    recent: stub('date.recent'),
    weekday: stub('date.weekday'),
  },
  finance: {
    account: stub('finance.account'),
    accountName: stub('finance.accountName'),
    amount: stub('finance.amount'),
    bic: stub('finance.bic'),
    bitcoinAddress: stub('finance.bitcoinAddress'),
    currencyCode: stub('finance.currencyCode'),
    currencyName: stub('finance.currencyName'),
    currencySymbol: stub('finance.currencySymbol'),
    iban: stub('finance.iban'),
    mask: stub('finance.mask'),
    transactionType: stub('finance.transactionType'),
  },
  hacker: {
    abbreviation: stub('hacker.abbreviation'),
    adjective: stub('hacker.adjective'),
    ingverb: stub('hacker.ingverb'),
    noun: stub('hacker.noun'),
    phrase: stub('hacker.phrase'),
    verb: stub('hacker.verb'),
  },
  image: {
    abstract: stub('image.abstract'),
    animals: stub('image.animals'),
    business: stub('image.business'),
    cats: stub('image.cats'),
    city: stub('image.city'),
    dataUri: stub('image.dataUri'),
    fashion: stub('image.fashion'),
    food: stub('image.food'),
    imageUrl: stub('image.imageUrl'),
    nature: stub('image.nature'),
    nightlife: stub('image.nightlife'),
    people: stub('image.people'),
    sports: stub('image.sports'),
    transport: stub('image.transport'),
  },
  internet: {
    color: stub('internet.color'),
    domainName: stub('internet.domainName'),
    domainSuffix: stub('internet.domainSuffix'),
    domainWord: stub('internet.domainWord'),
    email: stub('internet.email'),
    exampleEmail: stub('internet.exampleEmail'),
    ip: stub('internet.ip'),
    ipv: stub('internet.ipv'),
    mac: stub('internet.mac'),
    password: stub('internet.password'),
    protocol: stub('internet.protocol'),
    url: stub('internet.url'),
    userAgent: stub('internet.userAgent'),
    userName: stub('internet.userName'),
  },
  lorem: {
    lines: stub('lorem.lines'),
    paragraph: stub('lorem.paragraph'),
    paragraphs: stub('lorem.paragraphs'),
    sentence: stub('lorem.sentence'),
    sentences: stub('lorem.sentences'),
    slug: stub('lorem.slug'),
    text: stub('lorem.text'),
    word: stub('lorem.word'),
    words: stub('lorem.words'),
  },
  name: {
    findName: stub('name.findName'),
    firstName: stub('name.firstName'),
    jobArea: stub('name.jobArea'),
    jobDescriptor: stub('name.jobDescriptor'),
    jobTitle: stub('name.jobTitle'),
    jobType: stub('name.jobType'),
    lastName: stub('name.lastName'),
    prefix: stub('name.prefix'),
    suffix: stub('name.suffix'),
  },
  phone: {
    phoneNumber: stub('phone.phoneNumber'),
    phoneNumberFormat: stub('phone.phoneNumberFormat'),
  },
  random: {
    alphaNumeric: stub('random.alphaNumeric'),
    arrayElement: stub('random.arrayElement'),
    word: stub('random.word'),
  },
  system: {
    commonFileExt: stub('system.commonFileExt'),
    commonFileName: stub('system.commonFileName'),
    commonFileType: stub('system.commonFileType'),
    fileExt: stub('system.fileExt'),
    fileName: stub('system.fileName'),
    fileType: stub('system.fileType'),
    mimeType: stub('system.mimeType'),
    semver: stub('system.semver'),
  },
};

module.exports = faker;
module.exports.faker = faker;
module.exports.default = faker;
