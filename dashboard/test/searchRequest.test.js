// Unit tests for the Search view's request builder and validation. Run with `npm test` (node --test).
import { test, describe } from 'node:test'
import assert from 'node:assert/strict'

import {
  buildQuery, validateFields, validateSearch, searchShape, resultColumnFlags, buildLabelFilter,
  examplesForOperation, SEARCH_EXAMPLES, TAG_KEY_MAX_LENGTH
} from '../src/views/searchRequest.js'

// A minimal form: every field the view holds, at its initial value.
function form(overrides = {}) {
  return {
    embeddings: '', searchType: 'CosineSimilarity', minScore: '', maxScore: '', minDistance: '', maxDistance: '', vectorEfSearch: '',
    requiredLabels: [], excludedLabels: [], labelRequiredMode: 'All', requiredTags: [], excludedTags: [], requiredTerms: '', excludedTerms: '',
    sortOrder: 'ScoreDescending', maxResults: 10, includeNeighbors: '', includeEmbeddings: false,
    createdBefore: '', createdAfter: '', documentIds: '',
    fullTextQuery: '', fullTextSearchType: 'TsRank', fullTextMatchMode: 'Any', fullTextLanguage: 'english',
    fullTextNormalization: 32, fullTextMinScore: '', fullTextWeight: 0.5, fullTextMinimumShouldMatch: 1,
    hybridStrategy: 'Rrf', hybridRrfK: 60, hybridCandidatePool: '', hybridRecencyWeight: 0,
    collapseField: '', collapseTagKey: '', collapseCandidatePool: '',
    ...overrides
  }
}

const VECTOR = { embeddings: '0.1, 0.2, 0.3' }
const TEXT = { fullTextQuery: 'signing key rotation' }
const HYBRID = { ...VECTOR, ...TEXT }

function errorsOf(f) {
  return Object.entries(validateFields(f)).filter(([, v]) => v).map(([k]) => k)
}

describe('recency weight', () => {
  test('is sent on a hybrid Rrf search, including an explicit 0', () => {
    assert.equal(buildQuery(form({ ...HYBRID, hybridRecencyWeight: 0.1 })).Hybrid.RecencyWeight, 0.1)
    assert.equal(buildQuery(form({ ...HYBRID, hybridRecencyWeight: 0 })).Hybrid.RecencyWeight, 0)
    assert.equal(buildQuery(form({ ...HYBRID, hybridRecencyWeight: '1' })).Hybrid.RecencyWeight, 1)
  })

  test('is omitted when blank', () => {
    assert.equal('RecencyWeight' in buildQuery(form({ ...HYBRID, hybridRecencyWeight: '' })).Hybrid, false)
  })

  test('is not sent for Linear or Filter, or for single-leg searches', () => {
    assert.equal('RecencyWeight' in buildQuery(form({ ...HYBRID, hybridStrategy: 'Linear', hybridRecencyWeight: 0.3 })).Hybrid, false)
    assert.equal('RecencyWeight' in buildQuery(form({ ...HYBRID, hybridStrategy: 'Filter', hybridRecencyWeight: 0.3 })).Hybrid, false)
    assert.equal(buildQuery(form({ ...VECTOR, hybridRecencyWeight: 0.3 })).Hybrid, undefined)
    assert.equal(buildQuery(form({ ...TEXT, hybridRecencyWeight: 0.3 })).Hybrid, undefined)
  })

  test('accepts the range boundaries', () => {
    assert.deepEqual(errorsOf(form({ ...HYBRID, hybridRecencyWeight: 0 })), [])
    assert.deepEqual(errorsOf(form({ ...HYBRID, hybridRecencyWeight: 1 })), [])
  })

  test('rejects values outside 0-1 and non-numbers', () => {
    for (const bad of [1.5, -0.1, 'abc']) {
      assert.deepEqual(errorsOf(form({ ...HYBRID, hybridRecencyWeight: bad })), ['hybridRecencyWeight'], String(bad))
    }
  })

  test('is not validated when hidden (Linear strategy)', () => {
    assert.deepEqual(errorsOf(form({ ...HYBRID, hybridStrategy: 'Linear', hybridRecencyWeight: 5 })), [])
  })
})

describe('collapse', () => {
  test('is omitted when None', () => {
    assert.equal(buildQuery(form({ ...HYBRID })).Collapse, undefined)
  })

  test('by document ID sends only the field on a hybrid search', () => {
    assert.deepEqual(buildQuery(form({ ...HYBRID, collapseField: 'DocumentId', collapseCandidatePool: 50 })).Collapse, { Field: 'DocumentId' })
  })

  test('by tag sends the trimmed tag key', () => {
    assert.deepEqual(buildQuery(form({ ...HYBRID, collapseField: 'Tag', collapseTagKey: '  parentKey ' })).Collapse, { Field: 'Tag', TagKey: 'parentKey' })
  })

  test('sends the collapse pool on single-leg searches only', () => {
    assert.deepEqual(buildQuery(form({ ...VECTOR, collapseField: 'DocumentId', collapseCandidatePool: '40' })).Collapse, { Field: 'DocumentId', CandidatePool: 40 })
    assert.deepEqual(buildQuery(form({ ...TEXT, collapseField: 'DocumentId', collapseCandidatePool: '40' })).Collapse, { Field: 'DocumentId', CandidatePool: 40 })
    assert.equal('CandidatePool' in buildQuery(form({ ...VECTOR, collapseField: 'DocumentId', collapseCandidatePool: '' })).Collapse, false)
  })

  test('drops the tag key when collapsing by document ID', () => {
    assert.equal('TagKey' in buildQuery(form({ ...VECTOR, collapseField: 'DocumentId', collapseTagKey: 'parentKey' })).Collapse, false)
  })

  test('accepts valid combinations and boundaries', () => {
    assert.deepEqual(errorsOf(form({ ...HYBRID, collapseField: 'Tag', collapseTagKey: 'parentKey' })), [])
    assert.deepEqual(errorsOf(form({ ...HYBRID, hybridStrategy: 'Linear', collapseField: 'DocumentId' })), [])
    assert.deepEqual(errorsOf(form({ ...VECTOR, collapseField: 'Tag', collapseTagKey: 'k'.repeat(TAG_KEY_MAX_LENGTH) })), [])
    assert.deepEqual(errorsOf(form({ ...VECTOR, collapseField: 'DocumentId', collapseCandidatePool: 1 })), [])
    assert.deepEqual(errorsOf(form({ ...VECTOR, collapseField: 'DocumentId', collapseCandidatePool: 10000 })), [])
  })

  test('requires a tag key when collapsing by tag', () => {
    assert.deepEqual(errorsOf(form({ ...VECTOR, collapseField: 'Tag', collapseTagKey: '' })), ['collapseTagKey'])
    assert.deepEqual(errorsOf(form({ ...VECTOR, collapseField: 'Tag', collapseTagKey: '   ' })), ['collapseTagKey'])
  })

  test('rejects a tag key longer than 256 characters', () => {
    assert.deepEqual(errorsOf(form({ ...VECTOR, collapseField: 'Tag', collapseTagKey: 'k'.repeat(TAG_KEY_MAX_LENGTH + 1) })), ['collapseTagKey'])
  })

  test('rejects the Filter strategy', () => {
    assert.deepEqual(errorsOf(form({ ...HYBRID, hybridStrategy: 'Filter', collapseField: 'DocumentId' })), ['collapseField'])
  })

  test('rejects an unknown field', () => {
    assert.deepEqual(errorsOf(form({ ...VECTOR, collapseField: 'Chapter' })), ['collapseField'])
  })

  test('rejects a collapse pool outside 1-10000 or fractional', () => {
    for (const bad of [0, 10001, 2.5, 'x']) {
      assert.deepEqual(errorsOf(form({ ...VECTOR, collapseField: 'DocumentId', collapseCandidatePool: bad })), ['collapseCandidatePool'], String(bad))
    }
  })

  test('does not validate the collapse pool on hybrid searches, where it is hidden', () => {
    assert.deepEqual(errorsOf(form({ ...HYBRID, collapseField: 'DocumentId', collapseCandidatePool: 0 })), [])
  })
})

describe('minimum should match', () => {
  test('is sent when above 1 with match mode Any', () => {
    assert.equal(buildQuery(form({ ...TEXT, fullTextMinimumShouldMatch: 2 })).FullText.MinimumShouldMatch, 2)
    assert.equal(buildQuery(form({ ...HYBRID, fullTextMinimumShouldMatch: 3 })).FullText.MinimumShouldMatch, 3)
  })

  test('is omitted at the default of 1', () => {
    assert.equal('MinimumShouldMatch' in buildQuery(form({ ...TEXT, fullTextMinimumShouldMatch: 1 })).FullText, false)
  })

  test('is not sent for other match modes, so the server never sees an invalid combination', () => {
    for (const mode of ['All', 'Phrase', 'WebSearch']) {
      const q = buildQuery(form({ ...TEXT, fullTextMatchMode: mode, fullTextMinimumShouldMatch: 2 }))
      assert.equal('MinimumShouldMatch' in q.FullText, false, mode)
      assert.deepEqual(errorsOf(form({ ...TEXT, fullTextMatchMode: mode, fullTextMinimumShouldMatch: 2 })), [], mode)
    }
  })

  test('rejects values outside 1-3', () => {
    for (const bad of [0, 4, 1.5]) {
      assert.deepEqual(errorsOf(form({ ...TEXT, fullTextMinimumShouldMatch: bad })), ['fullTextMinimumShouldMatch'], String(bad))
    }
  })

  test('is not sent without a full-text query', () => {
    assert.equal(buildQuery(form({ ...VECTOR, fullTextMinimumShouldMatch: 2 })).FullText, undefined)
  })
})

describe('vector EfSearch', () => {
  test('is sent when set, including the range edges', () => {
    assert.equal(buildQuery(form({ ...VECTOR, vectorEfSearch: '200' })).Vector.EfSearch, 200)
    assert.equal(buildQuery(form({ ...VECTOR, vectorEfSearch: 1 })).Vector.EfSearch, 1)
    assert.equal(buildQuery(form({ ...HYBRID, vectorEfSearch: 1000 })).Vector.EfSearch, 1000)
    assert.deepEqual(errorsOf(form({ ...VECTOR, vectorEfSearch: 1 })), [])
    assert.deepEqual(errorsOf(form({ ...VECTOR, vectorEfSearch: 1000 })), [])
  })

  test('is omitted when blank, so the server picks the default', () => {
    assert.equal('EfSearch' in buildQuery(form({ ...VECTOR, vectorEfSearch: '' })).Vector, false)
  })

  test('rejects values outside 1-1000, fractions, and non-numbers', () => {
    for (const bad of [0, 1001, 2.5, 'x']) {
      assert.deepEqual(errorsOf(form({ ...VECTOR, vectorEfSearch: bad })), ['vectorEfSearch'], String(bad))
    }
  })

  test('is ignored without a vector', () => {
    assert.deepEqual(errorsOf(form({ ...TEXT, vectorEfSearch: 0 })), [])
    assert.equal(buildQuery(form({ ...TEXT, vectorEfSearch: 50 })).Vector, undefined)
  })
})

describe('label required mode', () => {
  test('sends All by default and Any when chosen, with the required labels', () => {
    assert.deepEqual(buildQuery(form({ ...VECTOR, requiredLabels: ['a', 'b'] })).LabelFilter, { Required: ['a', 'b'], RequiredMode: 'All' })
    assert.deepEqual(buildQuery(form({ ...VECTOR, requiredLabels: ['a', 'b'], labelRequiredMode: 'Any' })).LabelFilter, { Required: ['a', 'b'], RequiredMode: 'Any' })
  })

  test('is not sent without required labels', () => {
    assert.deepEqual(buildQuery(form({ ...VECTOR, excludedLabels: ['x'], labelRequiredMode: 'Any' })).LabelFilter, { Excluded: ['x'] })
    assert.equal(buildQuery(form({ ...VECTOR, labelRequiredMode: 'Any' })).LabelFilter, undefined)
  })

  test('rejects an unknown mode', () => {
    assert.deepEqual(errorsOf(form({ ...VECTOR, labelRequiredMode: 'Some' })), ['labelRequiredMode'])
  })

  test('buildLabelFilter builds the enumeration filter the same way', () => {
    assert.deepEqual(buildLabelFilter(['a', ' ', 'b'], [], 'Any'), { Required: ['a', 'b'], RequiredMode: 'Any' })
    assert.deepEqual(buildLabelFilter(['a'], ['z'], undefined), { Required: ['a'], RequiredMode: 'All', Excluded: ['z'] })
    assert.deepEqual(buildLabelFilter([], ['z'], 'Any'), { Excluded: ['z'] })
    assert.equal(buildLabelFilter([], [], 'Any'), null)
    assert.equal(buildLabelFilter(undefined, undefined), null)
  })
})

describe('include embeddings', () => {
  test('sends true when checked', () => {
    assert.equal(buildQuery(form({ ...VECTOR, includeEmbeddings: true })).IncludeEmbeddings, true)
  })

  test('is omitted when unchecked', () => {
    assert.equal('IncludeEmbeddings' in buildQuery(form({ ...VECTOR, includeEmbeddings: false })), false)
  })
})

describe('existing behavior', () => {
  test('a plain hybrid search builds the same body as before these fields existed', () => {
    assert.deepEqual(buildQuery(form({ ...HYBRID, hybridRecencyWeight: '' })), {
      SortOrder: 'ScoreDescending',
      MaxResults: 10,
      Vector: { SearchType: 'CosineSimilarity', Embeddings: [0.1, 0.2, 0.3] },
      FullText: { Query: 'signing key rotation', SearchType: 'TsRank', MatchMode: 'Any', Language: 'english', Normalization: 32, TextWeight: 0.5 },
      Hybrid: { Strategy: 'Rrf', RrfK: 60 }
    })
  })

  test('a text weight of 0 stays 0', () => {
    assert.equal(buildQuery(form({ ...HYBRID, fullTextWeight: 0 })).FullText.TextWeight, 0)
  })
})

describe('validateSearch', () => {
  test('passes a valid collapsed hybrid search with recency', () => {
    const r = validateSearch(form({ ...HYBRID, hybridRecencyWeight: 0.1, collapseField: 'Tag', collapseTagKey: 'parentKey' }), 3)
    assert.equal(r.error, null)
  })

  test('requires a vector or a text query', () => {
    assert.match(validateSearch(form({ collapseField: 'DocumentId' }), 3).error, /Embeddings or a full-text query are required/)
  })

  test('rejects a dimensionality mismatch', () => {
    assert.match(validateSearch(form({ ...VECTOR }), 4).error, /does not match the collection dimensionality/)
  })

  test('names every invalid field in the submit error', () => {
    const r = validateSearch(form({ ...HYBRID, hybridRecencyWeight: 2, collapseField: 'Tag' }), 3)
    assert.match(r.error, /Recency Weight: Must be between 0 and 1\./)
    assert.match(r.error, /Tag Key: Required when collapsing by tag\./)
  })
})

describe('searchShape', () => {
  test('shows recency only for hybrid Rrf and the collapse pool only for single-leg collapse', () => {
    assert.equal(searchShape(form({ ...HYBRID })).showRecencyWeight, true)
    assert.equal(searchShape(form({ ...HYBRID, hybridStrategy: 'Linear' })).showRecencyWeight, false)
    assert.equal(searchShape(form({ ...VECTOR })).showRecencyWeight, false)
    assert.equal(searchShape(form({ ...VECTOR, collapseField: 'DocumentId' })).showCollapsePool, true)
    assert.equal(searchShape(form({ ...HYBRID, collapseField: 'DocumentId' })).showCollapsePool, false)
    assert.equal(searchShape(form({ ...VECTOR })).showCollapsePool, false)
  })
})

describe('resultColumnFlags', () => {
  test('shows group and recency columns only when a hit carries them', () => {
    const plain = resultColumnFlags([{ Score: 1 }], false)
    assert.equal(plain.showGroup, false)
    assert.equal(plain.showRecencyRank, false)
    const grouped = resultColumnFlags([{ GroupKey: 'p-new', GroupHits: 3, RecencyRank: 1 }], true)
    assert.equal(grouped.showGroup, true)
    assert.equal(grouped.showRecencyRank, true)
  })

  test('handles a missing document list', () => {
    assert.deepEqual(resultColumnFlags(undefined, false), { showVectorScore: false, showRanks: false, showGroup: false, showRecencyRank: false })
  })
})

describe('API Explorer examples', () => {
  const searchPath = '/v1.0/tenants/{tid}/collections/{cid}/search'

  test('are offered for the search operation', () => {
    assert.equal(examplesForOperation('POST', searchPath).length, SEARCH_EXAMPLES.length)
    assert.equal(examplesForOperation('post', searchPath).length, SEARCH_EXAMPLES.length)
  })

  test('are not offered for other operations', () => {
    assert.deepEqual(examplesForOperation('GET', searchPath), [])
    assert.deepEqual(examplesForOperation('POST', '/v1.0/tenants/{tid}/collections/{cid}/documents'), [])
    assert.deepEqual(examplesForOperation('POST', '/v1.0/tenants/{tid}/collections/{cid}/search/other'), [])
    assert.deepEqual(examplesForOperation(undefined, undefined), [])
  })

  test('the collapse-with-recency example matches the documented single-call request', () => {
    const body = SEARCH_EXAMPLES[0].body
    assert.deepEqual(body.Hybrid, { Strategy: 'Rrf', RrfK: 60, CandidatePool: 40, RecencyWeight: 0.1 })
    assert.deepEqual(body.Collapse, { Field: 'Tag', TagKey: 'parentKey' })
  })

  test('every example would pass the dashboard validation rules', () => {
    for (const ex of SEARCH_EXAMPLES) {
      const b = ex.body
      const f = form({
        embeddings: b.Vector ? b.Vector.Embeddings.join(',') : '',
        fullTextQuery: b.FullText ? b.FullText.Query : '',
        fullTextMinimumShouldMatch: b.FullText?.MinimumShouldMatch ?? 1,
        hybridRecencyWeight: b.Hybrid?.RecencyWeight ?? '',
        hybridCandidatePool: b.Hybrid?.CandidatePool ?? '',
        collapseField: b.Collapse?.Field ?? '',
        collapseTagKey: b.Collapse?.TagKey ?? '',
        collapseCandidatePool: b.Collapse?.CandidatePool ?? ''
      })
      assert.equal(validateSearch(f, 3).error, null, ex.label)
    }
  })
})
