import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { TransactionsService, Transaction } from '../services/transactions.service';
import { AccountsService, Account } from '../services/accounts.service';
import { TransactionUploadsService } from '../services/transaction_uploads.service';
import { UtilsService } from '../services/utils.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-transactions',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './transactions.component.html',
  styleUrls: ['./transactions.component.css']
})
export class TransactionsComponent implements OnInit {
  @ViewChild('fileInput', { static: false }) fileInput!: ElementRef<HTMLInputElement>;
  transactions: Transaction[] = [];
  paginatedTransactions: Transaction[] = [];
  accounts: Account[] = [];
  // Constants
  readonly PAGE_SIZE_OPTIONS = [10, 25, 50];
  readonly MAX_PAGES_TO_SHOW = 5;
  readonly MAX_FILE_SIZE_BYTES = 50 * 1024 * 1024; // 50MB

  // Transactions pagination
  currentPage: number = 1;
  pageSize: number = 10;
  totalPages: number = 1;
  
  // Comparison results
  uploadAction: string = 'transactions';
  dataComparisonResults: any[] = [];
  
  // UI state
  isTransactionsExpanded: boolean = true;
  isComparisonResultsExpanded: boolean = true;

  constructor(
    private transactionsService: TransactionsService,
    private accountsService: AccountsService,
    private transactionUploadsService: TransactionUploadsService,
    private utilsService: UtilsService
  ) {}

  ngOnInit() {
    this.loadData();
  }

  private loadData() {
    this.transactionsService.getAllTransactions().subscribe({
      next: (data) => {
        this.transactions = this.sortTransactions(data);
        this.updatePagination();
      },
      error: (error) => {
        console.error('Error fetching transactions:', error);
      }
    });

    this.accountsService.getAccounts().subscribe({
      next: (data) => {
        this.accounts = data;
      },
      error: (error) => {
        console.error('Error fetching accounts:', error);
      }
    });
  }

  private updatePagination() {
    this.totalPages = Math.ceil(this.transactions.length / this.pageSize);
    if (this.currentPage > this.totalPages && this.totalPages > 0) {
      this.currentPage = this.totalPages;
    }
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    this.paginatedTransactions = this.transactions.slice(startIndex, endIndex);
  }

  onPageSizeChange() {
    this.currentPage = 1;
    this.updatePagination();
  }

  goToPage(page: number) {
    if (page >= 1 && page <= this.totalPages) {
      this.currentPage = page;
      this.updatePagination();
    }
  }

  previousPage() {
    if (this.currentPage > 1) {
      this.currentPage--;
      this.updatePagination();
    }
  }

  nextPage() {
    if (this.currentPage < this.totalPages) {
      this.currentPage++;
      this.updatePagination();
    }
  }

  getPageNumbers(): number[] {
    return this.calculatePageNumbers(this.currentPage, this.totalPages);
  }

  private calculatePageNumbers(currentPage: number, totalPages: number): number[] {
    const pages: number[] = [];
    let startPage = Math.max(1, currentPage - Math.floor(this.MAX_PAGES_TO_SHOW / 2));
    let endPage = Math.min(totalPages, startPage + this.MAX_PAGES_TO_SHOW - 1);
    
    if (endPage - startPage < this.MAX_PAGES_TO_SHOW - 1) {
      startPage = Math.max(1, endPage - this.MAX_PAGES_TO_SHOW + 1);
    }
    
    for (let i = startPage; i <= endPage; i++) {
      pages.push(i);
    }
    return pages;
  }

  // Expose Math to template
  Math = Math;

  private sortTransactions(transactions: Transaction[]): Transaction[] {
    return [...transactions].sort((a, b) => {
      const dateA = new Date(a.transaction_date).getTime();
      const dateB = new Date(b.transaction_date).getTime();
      
      if (dateB !== dateA) {
        return dateB - dateA;
      }

      return a.id - b.id;
    });
  }

  formatDate(dateString: string): string {
    return this.utilsService.formatDateMmddyyyy(dateString);
  }

  formatTransactionAmount(amount: number, direction: string): string {
    const displayAmount = direction === 'Outgoing' ? -amount : amount;
    return this.utilsService.formatCurrency(displayAmount);
  }

  getAmountClass(direction: string): string {
    if (direction === 'Outgoing') {
      return 'outgoing-amount';
    } else if (direction === 'Incoming') {
      return 'incoming-amount';
    }
    return '';
  }

  getAccountName(accountId: number): string {
    return this.findAccountById(accountId)?.name || '';
  }

  downloadSampleCsv() {
    const header = 'id,account_number,direction,amount,transaction_date\n';
    const row1 = 'sample_1,3,outgoing,1234.56,12/31/2024\n';
    const row2 = 'sample_2,1,incoming,543.21,2025-01-01\n';
    const csvContent = header + row1 + row2;

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);
    
    link.setAttribute('href', url);
    link.setAttribute('download', 'sample_transactions.csv');
    link.style.visibility = 'hidden';
    
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    
    // Clean up the URL object
    URL.revokeObjectURL(url);
  }

  uploadTransactions() {
    this.uploadAction = 'transactions';
    this.fileInput.nativeElement.click();
  }

  uploadGoldenCSV() {
    this.uploadAction = 'golden';
    this.fileInput.nativeElement.click();
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (!file.name.toLowerCase().endsWith('.csv')) {
      alert('Please select a CSV file');
      this.resetFileInput();
      return;
    }

    if (file.size > this.MAX_FILE_SIZE_BYTES) {
      alert(`File size exceeds the maximum limit of ${this.MAX_FILE_SIZE_BYTES / (1024 * 1024)}MB. Please select a smaller file.`);
      this.resetFileInput();
      return;
    }

    if (this.uploadAction === 'golden') {
      this.parseCsvFile(file).then(parsedData => {
        this.compareTransactionsToUploaded(parsedData);
      }).catch(error => {
        console.error('Error parsing CSV:', error);
        alert('Error parsing CSV file: ' + error.message);
        this.resetFileInput();
      });
    } else {
      this.transactionUploadsService.uploadCsv(file).subscribe({
        next: (response: any) => {
          alert(response.message || 'File uploaded successfully');
          this.loadData();
          this.resetFileInput();
        },
        error: (error) => {
          const errorMessage = error.error?.message || 'Error uploading file. Please try again.';
          alert(errorMessage);
          this.resetFileInput();
        }
      });
    }
  }

  toggleTransactionsSection() {
    this.isTransactionsExpanded = !this.isTransactionsExpanded;
  }

  toggleComparisonResultsSection() {
    this.isComparisonResultsExpanded = !this.isComparisonResultsExpanded;
  }

  getAccountNameFromNumber(accountNumber: string): string {
    const account = this.accounts.find(acc => acc.account_number === accountNumber);
    return account?.name || accountNumber;
  }

  getAccountNameFromId(accountId: number): string {
    return this.findAccountById(accountId)?.name || '';
  }

  private findAccountById(accountId: number): Account | undefined {
    return this.accounts.find(acc => acc.id === accountId);
  }

  normalizeDirectionForDisplay(direction: string): string {
    const normalized = this.normalizeDirection(direction);
    return normalized || direction;
  }

  formatComparisonAmountWithDirection(amount: string | number, direction: string): string {
    const numAmount = this.parseAmount(amount);
    if (isNaN(numAmount)) return String(amount);
    const normalizedDirection = this.normalizeDirection(direction);
    const displayAmount = normalizedDirection === 'Outgoing' ? -Math.abs(numAmount) : Math.abs(numAmount);
    return this.utilsService.formatCurrency(displayAmount);
  }

  private parseAmount(amount: string | number): number {
    return typeof amount === 'string' ? parseFloat(amount) : amount;
  }

  private normalizeDate(dateStr: string): string {
    const parsedDate = this.parseDate(dateStr);
    if (parsedDate) {
      return this.formatDateYyyyMmDd(parsedDate);
    }
    return '';
  }

  private parseDate(dateStr: string): Date | null {
    if (!dateStr) return null;
    
    dateStr = dateStr.trim();
    
    // Handle yyyy-MM-dd format explicitly to avoid timezone issues
    if (/^\d{4}-\d{2}-\d{2}$/.test(dateStr)) {
      const parts = dateStr.split('-');
      if (parts.length === 3) {
        const year = parseInt(parts[0], 10);
        const month = parseInt(parts[1], 10) - 1;
        const day = parseInt(parts[2], 10);
        const date = new Date(year, month, day);
        if (!isNaN(date.getTime())) {
          return date;
        }
      }
    }
    
    // Handle MM/dd/yyyy format
    if (/^\d{1,2}\/\d{1,2}\/\d{4}$/.test(dateStr)) {
      const parts = dateStr.split('/');
      if (parts.length === 3) {
        const month = parseInt(parts[0], 10) - 1;
        const day = parseInt(parts[1], 10);
        const year = parseInt(parts[2], 10);
        const date = new Date(year, month, day);
        if (!isNaN(date.getTime())) {
          return date;
        }
      }
    }
    
    // Fallback to standard Date parsing (handles ISO format with time)
    const date = new Date(dateStr);
    return isNaN(date.getTime()) ? null : date;
  }

  private formatDateYyyyMmDd(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    
    return `${year}-${month}-${day}`;
  }

  checkColumnMatch(columnName: string, csvResult: any, transaction: Transaction): boolean {
    const csvData = csvResult._csvData;
    if (!csvData) return false;

    switch(columnName) {
      case 'transaction_date':
        const normalizedCsvDate = this.normalizeDate(csvData.transaction_date);
        const normalizedTransactionDate = this.normalizeDate(transaction.transaction_date);
        // Compare both normalized dates
        if (!normalizedCsvDate || !normalizedTransactionDate) return false;
        return normalizedCsvDate === normalizedTransactionDate;
      case 'account':
        const csvAccountNumber = csvData.account_number || '';
        const transactionAccount = this.accounts.find(acc => acc.id === transaction.account_id);
        return transactionAccount ? transactionAccount.account_number === csvAccountNumber : false;
      case 'external_transaction_id':
        return csvData.id === transaction.external_transaction_id;
      case 'direction':
        const normalizedCsvDirection = this.normalizeDirection(csvData.direction);
        return normalizedCsvDirection === transaction.direction;
      case 'amount':
        const csvAmount = Math.abs(parseFloat(csvData.amount));
        return csvAmount === transaction.amount;
      default:
        return false;
    }
  }

  private isPerfectMatch(
    transaction: Transaction,
    csvId: string,
    csvAmount: number,
    accountId: number,
    normalizedDate: string,
    normalizedDirection: string,
    normalizedTransactionDate: string | null
  ): boolean {
    if (!normalizedTransactionDate) return false;
    return transaction.external_transaction_id === csvId
      && transaction.amount === csvAmount
      && transaction.account_id === accountId
      && normalizedTransactionDate === normalizedDate
      && transaction.direction === normalizedDirection;
  }

  private findPerfectMatches(
    csvTransaction: any,
    csvAmount: number,
    accountId: number,
    normalizedDate: string,
    normalizedDirection: string
  ): Transaction[] {
    return this.transactions.filter(t => {
      const normalizedTransactionDate = this.normalizeDate(t.transaction_date);
      return this.isPerfectMatch(t, csvTransaction.id, csvAmount, accountId, normalizedDate, normalizedDirection, normalizedTransactionDate);
    });
  }

  private compareTransactionsToUploaded(parsedData: any[]) {
    this.dataComparisonResults = [];

    const accountNumberToIdMap = new Map<string, number>();
    this.accounts.forEach(account => {
      accountNumberToIdMap.set(account.account_number, account.id);
    });

    parsedData.forEach(csvTransaction => {
      const accountNumber = csvTransaction.account_number;
      const accountId = accountNumberToIdMap.get(accountNumber);
      const accountName = this.getAccountNameFromNumber(accountNumber);
      
      const csvAmount = Math.abs(parseFloat(csvTransaction.amount));
      const normalizedDate = this.normalizeDate(csvTransaction.transaction_date);
      const normalizedDirection = this.normalizeDirection(csvTransaction.direction);

      // Initialize result object with CSV data
      const result: any = {
        transaction_date: csvTransaction.transaction_date,
        account: accountName || accountNumber,
        external_transaction_id: csvTransaction.id,
        direction: csvTransaction.direction,
        amount: csvTransaction.amount,
        perfect_match: false,
        matching_transaction_id: [],
        near_matches: [],
        // Store original CSV data for comparison
        _csvData: {
          account_number: accountNumber,
          transaction_date: csvTransaction.transaction_date,
          id: csvTransaction.id,
          direction: csvTransaction.direction,
          amount: csvTransaction.amount
        }
      };

      // Skip validation checks if data is invalid, but still add to results
      if (!accountId || isNaN(csvAmount) || !normalizedDate || !normalizedDirection) {
        this.dataComparisonResults.push(result);
        return;
      }

      // Check for perfect matches
      const perfectMatches = this.findPerfectMatches(csvTransaction, csvAmount, accountId, normalizedDate, normalizedDirection);

      if (perfectMatches.length > 0) {
        result.perfect_match = true;
        result.matching_transaction_id = perfectMatches.map(t => t.id);
        this.dataComparisonResults.push(result);
        return;
      }

      // Check for near matches (all variations) - exclude perfect matches
      const nearMatches = this.transactions.filter(t => {
        const normalizedTransactionDate = this.normalizeDate(t.transaction_date);
        if (!normalizedTransactionDate) return false;
        
        // Skip if this is a perfect match
        if (this.isPerfectMatch(t, csvTransaction.id, csvAmount, accountId, normalizedDate, normalizedDirection, normalizedTransactionDate)) {
          return false;
        }
        
        // Check for 4 out of 5 matches
        return (t.external_transaction_id === csvTransaction.id
          && t.amount === csvAmount
          && t.account_id === accountId
          && normalizedTransactionDate === normalizedDate
          && t.direction !== normalizedDirection) ||
        (t.external_transaction_id === csvTransaction.id
          && t.amount === csvAmount
          && t.account_id === accountId
          && normalizedTransactionDate !== normalizedDate
          && t.direction === normalizedDirection) ||
        (t.external_transaction_id === csvTransaction.id
          && t.amount === csvAmount
          && t.account_id !== accountId
          && normalizedTransactionDate === normalizedDate
          && t.direction === normalizedDirection) ||
        (t.external_transaction_id === csvTransaction.id
          && t.amount !== csvAmount
          && t.account_id === accountId
          && normalizedTransactionDate === normalizedDate
          && t.direction === normalizedDirection) ||
        (t.external_transaction_id !== csvTransaction.id
          && t.amount === csvAmount
          && t.account_id === accountId
          && normalizedTransactionDate === normalizedDate
          && t.direction === normalizedDirection);
      });

      if (nearMatches.length > 0) {
        result.near_matches = nearMatches;
      }

      console.log('csvTransaction', csvTransaction);
      console.log('nearMatches', nearMatches);

      this.dataComparisonResults.push(result);
    });
  }

  private normalizeDirection(direction: string): string | null {
    if (!direction) return null;
    let formattedDirection = direction.trim().toLowerCase();
    if (formattedDirection === 'outgoing' || formattedDirection === 'o') {
      return 'Outgoing';
    } else if (formattedDirection === 'incoming' || formattedDirection === 'i') {
      return 'Incoming';
    }
    return null;
  }

  private resetFileInput() {
    if (this.fileInput) {
      this.fileInput.nativeElement.value = '';
    }
  }

  private parseCsvFile(file: File): Promise<any[]> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      
      reader.onload = (e) => {
        try {
          const text = e.target?.result as string;
          const lines = text.split('\n').filter(line => line.trim() !== '');
          
          if (lines.length < 2) {
            reject(new Error('CSV file must contain at least a header row and one data row'));
            return;
          }

          const headerLine = lines[0].trim();
          const headers = this.parseCsvLine(headerLine);
          const headerMap = new Map<string, number>();
          headers.forEach((header, index) => {
            headerMap.set(header.trim().toLowerCase(), index);
          });

          const requiredColumns = ['id', 'account_number', 'direction', 'amount', 'transaction_date'];
          const missingColumns = requiredColumns.filter(col => !headerMap.has(col));
          if (missingColumns.length > 0) {
            reject(new Error(`Missing required columns: ${missingColumns.join(', ')}`));
            return;
          }

          const parsedData: any[] = [];
          for (let i = 1; i < lines.length; i++) {
            const line = lines[i].trim();
            if (!line) continue;

            const values = this.parseCsvLine(line);
            const row: any = {};
            
            headerMap.forEach((index, headerName) => {
              if (index < values.length) {
                row[headerName] = values[index]?.trim() || '';
              }
            });
            
            parsedData.push(row);
          }

          resolve(parsedData);
        } catch (error) {
          reject(error);
        }
      };

      reader.onerror = () => {
        reject(new Error('Failed to read file'));
      };

      reader.readAsText(file);
    });
  }

  private parseCsvLine(line: string): string[] {
    const values: string[] = [];
    let current = '';
    let inQuotes = false;

    for (let i = 0; i < line.length; i++) {
      const c = line[i];
      
      if (c === '"') {
        inQuotes = !inQuotes;
      } else if (c === ',' && !inQuotes) {
        values.push(current);
        current = '';
      } else {
        current += c;
      }
    }
    
    values.push(current);
    return values;
  }
}