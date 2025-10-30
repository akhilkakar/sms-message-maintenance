import React, { useState, useEffect } from 'react';
import axios from 'axios';
import './App.css';

interface Message {
  id: number;
  to: string;
  from: string;
  message: string;
  status: string;
  createdDateTime: string;
  modifiedDateTime: string;
}

interface PaginatedResponse {
  data: Message[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

const App: React.FC = () => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [sortBy, setSortBy] = useState('createdDateTime');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // API base URL - replace with your Azure API Management endpoint
  const API_BASE_URL = process.env.REACT_APP_API_URL || 'https://your-apim.azure-api.net/api';
  const API_KEY = process.env.REACT_APP_API_KEY || '';

 // console.log("🧭 API_BASE_URL =", process.env.REACT_APP_API_URL);
  //console.log("🔑 API_KEY =", process.env.REACT_APP_API_KEY);


  useEffect(() => {
    // Load all messages on initial page load (no search)
    if (searchTerm.length === 0) {
      fetchAllMessages();
      return;
    }
    // When searching (>=3 chars), use normal paginated fetch
    if (searchTerm.length >= 3) {
      fetchMessages();
    }
  }, [searchTerm, currentPage, sortBy, sortOrder]);

  const fetchMessages = async () => {
    setLoading(true);
    setError(null);
    
    try {
      const params = {
        search: searchTerm,
        page: currentPage,
        pageSize: pageSize,
        sortBy: sortBy,
        sortOrder: sortOrder
      };

      const headers = API_KEY ? { 'Ocp-Apim-Subscription-Key': API_KEY } : {};
      const response = await axios.get<PaginatedResponse>(`${API_BASE_URL}/messages`, { params, headers });
      
      setMessages(response.data.data);
      setTotalPages(response.data.totalPages);
      setTotalCount(response.data.totalCount);
    } catch (err) {
      setError('Failed to fetch messages. Please try again.');
      console.error('Error fetching messages:', err);
    } finally {
      setLoading(false);
    }
  };

  const fetchAllMessages = async () => {
    setLoading(true);
    setError(null);
    try {
      const baseParams = {
        search: '',
        page: 1,
        pageSize: 10,
        sortBy: sortBy,
        sortOrder: sortOrder
      };
      const headers = API_KEY ? { 'Ocp-Apim-Subscription-Key': API_KEY } : {};

      const first = await axios.get<PaginatedResponse>(`${API_BASE_URL}/messages`, { params: baseParams, headers });
      const pages = first.data.totalPages || 1;

      let all = first.data.data;
      if (pages > 1) {
        const rest = await Promise.all(
          Array.from({ length: pages - 1 }, (_, i) =>
            axios.get<PaginatedResponse>(`${API_BASE_URL}/messages`, {
              params: { ...baseParams, page: i + 2 },
              headers
            })
          )
        );
        all = [
          ...all,
          ...rest.flatMap(r => r.data.data)
        ];
      }

      setMessages(all);
      setTotalCount(all.length);
      setTotalPages(1);
      setCurrentPage(1);
    } catch (err) {
      setError('Failed to fetch all messages. Please try again.');
      console.error('Error fetching all messages:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchTerm(e.target.value);
    setCurrentPage(1); // Reset to first page on new search
  };

  const handleSort = (field: string) => {
    if (sortBy === field) {
      // Toggle sort order if clicking same field
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      // New field, default to descending
      setSortBy(field);
      setSortOrder('desc');
    }
    setCurrentPage(1); // Reset to first page on sort change
  };

  const handlePageChange = (newPage: number) => {
    if (newPage >= 1 && newPage <= totalPages) {
      setCurrentPage(newPage);
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString();
  };

  const getStatusColor = (status: string) => {
    if (status.includes('Successfully')) return 'status-success';
    if (status.includes('Not Sent')) return 'status-error';
    if (status.includes('Queued') || status.includes('Processing')) return 'status-pending';
    return 'status-default';
  };

  const renderSortIcon = (field: string) => {
    if (sortBy !== field) return <span className="sort-icon">⇅</span>;
    return sortOrder === 'asc' ? <span className="sort-icon">↑</span> : <span className="sort-icon">↓</span>;
  };

  return (
    <div className="app-container">
      <header className="app-header">
        <h1>📱 SMS Message Management</h1>
        <p className="subtitle">View and search message delivery status</p>
      </header>

      <div className="search-section">
        <div className="search-box">
          <span className="search-icon">🔍</span>
          <input
            type="text"
            placeholder="Search messages (min 3 characters)..."
            value={searchTerm}
            onChange={handleSearchChange}
            className="search-input"
          />
          {searchTerm && (
            <button 
              className="clear-button"
              onClick={() => setSearchTerm('')}
              aria-label="Clear search"
            >
              ✕
            </button>
          )}
        </div>
        {searchTerm.length > 0 && searchTerm.length < 3 && (
          <p className="search-hint">⚠️ Type at least 3 characters to search</p>
        )}
      </div>

      {error && (
        <div className="error-message">
          ⚠️ {error}
        </div>
      )}

      <div className="results-summary">
        <span className="total-count">Total Messages: <strong>{totalCount}</strong></span>
        <span className="page-info">Page {currentPage} of {totalPages}</span>
      </div>

      {loading ? (
        <div className="loading-spinner">
          <div className="spinner"></div>
          <p>Loading messages...</p>
        </div>
      ) : (
        <>
          <div className="table-container">
            <table className="messages-table">
              <thead>
                <tr>
                  <th onClick={() => handleSort('id')} className="sortable">
                    ID {renderSortIcon('id')}
                  </th>
                  <th>From</th>
                  <th>To</th>
                  <th onClick={() => handleSort('message')} className="sortable">
                    Message {renderSortIcon('message')}
                  </th>
                  <th onClick={() => handleSort('status')} className="sortable">
                    Status {renderSortIcon('status')}
                  </th>
                  <th onClick={() => handleSort('createdDateTime')} className="sortable">
                    Created {renderSortIcon('createdDateTime')}
                  </th>
                  <th onClick={() => handleSort('modifiedDateTime')} className="sortable">
                    Modified {renderSortIcon('modifiedDateTime')}
                  </th>
                </tr>
              </thead>
              <tbody>
                {messages.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="no-results">
                      {searchTerm ? '🔍 No messages found matching your search' : '📭 No messages available'}
                    </td>
                  </tr>
                ) : (
                  messages.map((message) => (
                    <tr key={message.id}>
                      <td className="id-cell">{message.id}</td>
                      <td className="phone-cell">{message.from}</td>
                      <td className="phone-cell">{message.to}</td>
                      <td className="message-cell" title={message.message}>
                        {message.message}
                      </td>
                      <td>
                        <span className={`status-badge ${getStatusColor(message.status)}`}>
                          {message.status || 'Pending'}
                        </span>
                      </td>
                      <td className="date-cell">{formatDate(message.createdDateTime)}</td>
                      <td className="date-cell">{formatDate(message.modifiedDateTime)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="pagination">
              <button
                onClick={() => handlePageChange(1)}
                disabled={currentPage === 1}
                className="pagination-button"
              >
                ⏮️ First
              </button>
              <button
                onClick={() => handlePageChange(currentPage - 1)}
                disabled={currentPage === 1}
                className="pagination-button"
              >
                ⬅️ Previous
              </button>
              
              <div className="page-numbers">
                {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                  let pageNum;
                  if (totalPages <= 5) {
                    pageNum = i + 1;
                  } else if (currentPage <= 3) {
                    pageNum = i + 1;
                  } else if (currentPage >= totalPages - 2) {
                    pageNum = totalPages - 4 + i;
                  } else {
                    pageNum = currentPage - 2 + i;
                  }
                  
                  return (
                    <button
                      key={pageNum}
                      onClick={() => handlePageChange(pageNum)}
                      className={`page-number ${currentPage === pageNum ? 'active' : ''}`}
                    >
                      {pageNum}
                    </button>
                  );
                })}
              </div>

              <button
                onClick={() => handlePageChange(currentPage + 1)}
                disabled={currentPage === totalPages}
                className="pagination-button"
              >
                Next ➡️
              </button>
              <button
                onClick={() => handlePageChange(totalPages)}
                disabled={currentPage === totalPages}
                className="pagination-button"
              >
                Last ⏭️
              </button>
            </div>
          )}
        </>
      )}

      <footer className="app-footer">
        <p>💡 Tip: Click column headers to sort • Enter 3+ characters to search</p>
      </footer>
    </div>
  );
};

export default App;
