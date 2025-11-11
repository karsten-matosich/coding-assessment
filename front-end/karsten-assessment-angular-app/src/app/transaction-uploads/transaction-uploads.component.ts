import { Component, OnInit } from '@angular/core';
import { TransactionUploadsService, TransactionUpload } from '../services/transaction_uploads.service';
import { FailedTransactionImportsService, FailedTransactionImport } from '../services/failed_transaction_imports.service';
import { UtilsService } from '../services/utils.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-transaction-uploads',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './transaction-uploads.component.html',
  styleUrls: ['./transaction-uploads.component.css']
})
export class TransactionUploadsComponent implements OnInit {
  // Constants
  readonly PAGE_SIZE_OPTIONS = [10, 25, 50];
  readonly MAX_PAGES_TO_SHOW = 5;
  private readonly BYTES_PER_KB = 1024;
  private readonly BYTES_PER_MB = 1024 * 1024;

  transactionUploads: TransactionUpload[] = [];
  failedImports: FailedTransactionImport[] = [];
  paginatedFailedImports: FailedTransactionImport[] = [];
  
  // UI state
  isExpanded: boolean = true;
  isFailedUploadsExpanded: boolean = false;
  
  // Failed imports pagination
  failedImportsCurrentPage: number = 1;
  failedImportsPageSize: number = 10;
  failedImportsTotalPages: number = 1;

  constructor(
    private transactionUploadsService: TransactionUploadsService,
    private failedTransactionImportsService: FailedTransactionImportsService,
    private utilsService: UtilsService
  ) {}

  toggleExpanded() {
    this.isExpanded = !this.isExpanded;
  }

  toggleFailedUploads() {
    this.isFailedUploadsExpanded = !this.isFailedUploadsExpanded;
    
    // Load failed imports if not already loaded
    if (this.isFailedUploadsExpanded && this.failedImports.length === 0) {
      this.loadFailedImports();
    }
  }

  private loadFailedImports() {
    this.failedTransactionImportsService.getFailedTransactionImports().subscribe({
      next: (data) => {
        this.failedImports = this.sortByIdDescending(data);
        this.updateFailedImportsPagination();
      },
      error: (error) => {
        console.error('Error fetching failed imports:', error);
      }
    });
  }

  private sortByIdDescending<T extends { id: number }>(items: T[]): T[] {
    return items.sort((a, b) => b.id - a.id);
  }

  private updateFailedImportsPagination() {
    this.failedImportsTotalPages = Math.ceil(this.failedImports.length / this.failedImportsPageSize);
    if (this.failedImportsCurrentPage > this.failedImportsTotalPages && this.failedImportsTotalPages > 0) {
      this.failedImportsCurrentPage = this.failedImportsTotalPages;
    }
    const startIndex = (this.failedImportsCurrentPage - 1) * this.failedImportsPageSize;
    const endIndex = startIndex + this.failedImportsPageSize;
    this.paginatedFailedImports = this.failedImports.slice(startIndex, endIndex);
  }

  onFailedImportsPageSizeChange() {
    this.failedImportsCurrentPage = 1;
    this.updateFailedImportsPagination();
  }

  goToFailedImportsPage(page: number) {
    if (page >= 1 && page <= this.failedImportsTotalPages) {
      this.failedImportsCurrentPage = page;
      this.updateFailedImportsPagination();
    }
  }

  previousFailedImportsPage() {
    if (this.failedImportsCurrentPage > 1) {
      this.failedImportsCurrentPage--;
      this.updateFailedImportsPagination();
    }
  }

  nextFailedImportsPage() {
    if (this.failedImportsCurrentPage < this.failedImportsTotalPages) {
      this.failedImportsCurrentPage++;
      this.updateFailedImportsPagination();
    }
  }

  getFailedImportsPageNumbers(): number[] {
    return this.calculatePageNumbers(this.failedImportsCurrentPage, this.failedImportsTotalPages);
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

  ngOnInit() {
    this.loadTransactionUploads();
  }

  private loadTransactionUploads() {
    this.transactionUploadsService.getTransactionUploads().subscribe({
      next: (data) => {
        this.transactionUploads = data;
      },
      error: (error) => {
        console.error('Error fetching transaction uploads:', error);
      }
    });
  }

  formatDate(dateString: string): string {
    return this.utilsService.formatDateMmddyyyy(dateString);
  }

  formatFileSize(bytes: number): string {
    if (bytes < this.BYTES_PER_KB) {
      return `${bytes} B`;
    } else if (bytes < this.BYTES_PER_MB) {
      return `${(bytes / this.BYTES_PER_KB).toFixed(2)} KB`;
    } else {
      return `${(bytes / this.BYTES_PER_MB).toFixed(2)} MB`;
    }
  }

  refresh() {
    this.loadTransactionUploads();
    
    // Refresh failed imports if section is expanded
    if (this.isFailedUploadsExpanded) {
      this.loadFailedImports();
    }
  }
}