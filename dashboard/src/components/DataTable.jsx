import React, { useState, useMemo, useEffect, useRef } from 'react'

export default function DataTable({
  data = [],
  columns = [],
  loading = false,
  pageSize: defaultPageSize = 10,
  onRowClick = null,
  hidePagination = false,
  onRefresh = null,
  refreshing = false,
  totalRecords = null,
  onPageChange = null,
  expandable = false,
  renderExpanded = null
}) {
  const isServerPaginated = totalRecords != null && onPageChange != null
  const [currentPage, setCurrentPage] = useState(0)
  const [pageSize, setPageSize] = useState(defaultPageSize)
  const [sortConfig, setSortConfig] = useState({ key: null, direction: 'asc' })
  const [filters, setFilters] = useState({})
  const [pageInput, setPageInput] = useState('1')
  const [expandedRows, setExpandedRows] = useState({})
  const [refreshDone, setRefreshDone] = useState(false)
  const prevRefreshing = useRef(false)

  useEffect(() => {
    if (prevRefreshing.current && !refreshing) {
      setRefreshDone(true)
      const timer = setTimeout(() => setRefreshDone(false), 1500)
      return () => clearTimeout(timer)
    }
    prevRefreshing.current = refreshing
  }, [refreshing])

  const filteredAndSortedData = useMemo(() => {
    let result = [...data]

    Object.keys(filters).forEach((key) => {
      const filterValue = filters[key]?.toLowerCase()
      if (filterValue) {
        const column = columns.find((col) => col.key === key)
        result = result.filter((item) => {
          const value = column?.filterValue
            ? column.filterValue(item)
            : item[key]
          return String(value || '').toLowerCase().includes(filterValue)
        })
      }
    })

    if (sortConfig.key) {
      const column = columns.find((col) => col.key === sortConfig.key)
      result.sort((a, b) => {
        const aValue = column?.sortValue ? column.sortValue(a) : a[sortConfig.key]
        const bValue = column?.sortValue ? column.sortValue(b) : b[sortConfig.key]

        if (aValue === null || aValue === undefined) return 1
        if (bValue === null || bValue === undefined) return -1

        const comparison = String(aValue).localeCompare(String(bValue), undefined, { numeric: true })
        return sortConfig.direction === 'asc' ? comparison : -comparison
      })
    }

    return result
  }, [data, columns, sortConfig, filters])

  const effectiveTotal = isServerPaginated ? totalRecords : filteredAndSortedData.length
  const totalPages = Math.max(1, Math.ceil(effectiveTotal / pageSize))
  const startIndex = currentPage * pageSize
  const endIndex = Math.min(startIndex + pageSize, effectiveTotal)
  const paginatedData = isServerPaginated ? data : filteredAndSortedData.slice(startIndex, endIndex)

  useEffect(() => {
    if (currentPage >= totalPages && totalPages > 0) {
      setCurrentPage(totalPages - 1)
      setPageInput(String(totalPages))
    }
  }, [currentPage, totalPages])

  const handleSort = (key) => {
    const column = columns.find((col) => col.key === key)
    if (column?.sortable === false) return

    setSortConfig((prev) => ({
      key,
      direction: prev.key === key && prev.direction === 'asc' ? 'desc' : 'asc'
    }))
  }

  const handleFilterChange = (key, value) => {
    setFilters((prev) => ({ ...prev, [key]: value }))
    setCurrentPage(0)
    setPageInput('1')
  }

  const goToPage = (page, newPageSize) => {
    const size = newPageSize || pageSize
    const maxPage = Math.max(0, Math.ceil(effectiveTotal / size) - 1)
    const validPage = Math.max(0, Math.min(page, maxPage))
    setCurrentPage(validPage)
    setPageInput(String(validPage + 1))
    if (onPageChange) onPageChange(validPage, size)
  }

  const handlePageInputChange = (e) => {
    setPageInput(e.target.value)
  }

  const handlePageInputSubmit = (e) => {
    if (e.key === 'Enter') {
      const pageNum = parseInt(pageInput, 10)
      if (!isNaN(pageNum) && pageNum >= 1 && pageNum <= totalPages) {
        goToPage(pageNum - 1)
      } else {
        setPageInput(String(currentPage + 1))
      }
    }
  }

  const getSortIcon = (key) => {
    if (sortConfig.key !== key) {
      return (
        <svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor" opacity="0.3">
          <path d="M6 2L9 5H3L6 2Z" />
          <path d="M6 10L3 7H9L6 10Z" />
        </svg>
      )
    }
    return sortConfig.direction === 'asc' ? (
      <svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor">
        <path d="M6 2L9 5H3L6 2Z" />
      </svg>
    ) : (
      <svg width="12" height="12" viewBox="0 0 12 12" fill="currentColor">
        <path d="M6 10L3 7H9L6 10Z" />
      </svg>
    )
  }

  if (loading) {
    return (
      <div className="data-table-loading">
        <div className="spinner"></div>
        <span>Loading...</span>
      </div>
    )
  }

  return (
    <div className="data-table-container">
      {!hidePagination && (
        <div className="pagination">
          <div className="pagination-info">
            Showing {effectiveTotal === 0 ? 0 : startIndex + 1} to{' '}
            {endIndex} of {effectiveTotal.toLocaleString()} entries
          </div>

          <div className="pagination-controls">
            <select
              value={pageSize}
              onChange={(e) => {
                const newSize = Number(e.target.value)
                setPageSize(newSize)
                goToPage(0, newSize)
              }}
            >
              <option value={10}>10</option>
              <option value={25}>25</option>
              <option value={50}>50</option>
              <option value={100}>100</option>
            </select>

            <button onClick={() => goToPage(0)} disabled={currentPage === 0}>
              First
            </button>
            <button onClick={() => goToPage(currentPage - 1)} disabled={currentPage === 0}>
              Prev
            </button>

            <span className="page-input-container">
              Page{' '}
              <input
                type="text"
                value={pageInput}
                onChange={handlePageInputChange}
                onKeyDown={handlePageInputSubmit}
                className="page-input"
              />{' '}
              of {totalPages}
            </span>

            <button onClick={() => goToPage(currentPage + 1)} disabled={currentPage >= totalPages - 1}>
              Next
            </button>
            <button onClick={() => goToPage(totalPages - 1)} disabled={currentPage >= totalPages - 1}>
              Last
            </button>

            {onRefresh && (
              <button
                className={`refresh-btn${refreshing ? ' spinning' : ''}${refreshDone ? ' done' : ''}`}
                onClick={onRefresh}
                disabled={refreshing}
                title="Refresh"
              >
                {refreshDone ? (
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                ) : (
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <polyline points="23 4 23 10 17 10" />
                    <path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10" />
                  </svg>
                )}
              </button>
            )}
          </div>
        </div>
      )}
      <table className="data-table">
        <thead>
          <tr>
            {expandable && <th style={{ width: 32 }}></th>}
            {columns.map((col) => (
              <th
                key={col.key}
                onClick={() => !col.isAction && handleSort(col.key)}
                className={col.sortable !== false && !col.isAction ? 'sortable' : ''}
                style={col.width ? { width: col.width, minWidth: col.width } : {}}
                title={col.tooltip || ''}
              >
                <div className="th-content">
                  <span>{col.label}</span>
                  {col.sortable !== false && !col.isAction && getSortIcon(col.key)}
                </div>
              </th>
            ))}
          </tr>
          {!isServerPaginated && (
            <tr className="filter-row">
              {expandable && <th></th>}
              {columns.map((col) => (
                <th key={`filter-${col.key}`}>
                  {col.filterable !== false && !col.isAction && (
                    <input
                      type="text"
                      placeholder="Filter..."
                      value={filters[col.key] || ''}
                      onChange={(e) => handleFilterChange(col.key, e.target.value)}
                    />
                  )}
                </th>
              ))}
            </tr>
          )}
        </thead>
        <tbody>
          {paginatedData.length === 0 ? (
            <tr>
              <td colSpan={expandable ? columns.length + 1 : columns.length} className="no-data">
                No data available
              </td>
            </tr>
          ) : (
            paginatedData.map((item, index) => {
              const rowKey = item.Id || item.DocumentKey || index
              const isExpanded = expandedRows[rowKey]
              const hasExpandContent = expandable && renderExpanded && renderExpanded(item)
              return (
                <React.Fragment key={rowKey}>
                  <tr
                    onClick={() => onRowClick && onRowClick(item)}
                    className={onRowClick ? 'clickable' : ''}
                  >
                    {expandable && (
                      <td style={{ width: 32, textAlign: 'center', cursor: hasExpandContent ? 'pointer' : 'default', padding: '4px' }}
                        onClick={(e) => { e.stopPropagation(); if (hasExpandContent) setExpandedRows(prev => ({ ...prev, [rowKey]: !prev[rowKey] })) }}>
                        {hasExpandContent ? (isExpanded ? '\u25B2' : '\u25BC') : ''}
                      </td>
                    )}
                    {columns.map((col) => (
                      <td key={col.key} style={col.width ? { minWidth: col.width } : undefined}>
                        {col.render ? col.render(item) : item[col.key]}
                      </td>
                    ))}
                  </tr>
                  {isExpanded && hasExpandContent && (
                    <tr>
                      <td colSpan={columns.length + 1} style={{ padding: 0 }}>
                        {renderExpanded(item)}
                      </td>
                    </tr>
                  )}
                </React.Fragment>
              )
            })
          )}
        </tbody>
      </table>
    </div>
  )
}
